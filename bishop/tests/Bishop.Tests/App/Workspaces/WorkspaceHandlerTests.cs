using Bishop.App.Cards.AddCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Services.Terminal;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.App.Workspaces.DeleteWorkspace;
using Bishop.App.Workspaces.GetWorkspace;
using Bishop.App.Workspaces.InitWorkspace;
using Bishop.App.Workspaces.LaunchPlainTerminal;
using Bishop.App.Workspaces.LaunchWorkspace;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.App.Workspaces.ReorderWorkspaces;
using Bishop.App.Workspaces.PurgeWorkspace;
using Bishop.App.Workspaces.RemoveWorkspace;
using Bishop.App.Workspaces.UpdateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Bishop.Tests.App.Workspaces;

public sealed class WorkspaceHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;

    public WorkspaceHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private InitWorkspaceCommandHandler CreateInitHandler() => new(_factory);

    [Fact]
    public async Task CreateWorkspace_PersistsAndReturnsWorkspace()
    {
        var name = U("MyRepo");
        var handler = new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp);

        var result = await handler.Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be(name);
        result.Path.Should().Be($@"C:\code\{name}");
        result.CreatedAt.Should().NotBe(default);
        result.UpdatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task CreateWorkspace_RelativePath_ThrowsArgumentException()
    {
        var handler = new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp);

        var act = () => handler.Handle(new CreateWorkspaceCommand("MyRepo", @"code\myrepo"), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute*");
    }

    [Fact]
    public async Task CreateWorkspace_TraversalPath_ThrowsArgumentException()
    {
        var handler = new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp);

        var act = () => handler.Handle(new CreateWorkspaceCommand("MyRepo", @"C:\code\..\sensitive"), default);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*traversal*");
    }

    [Fact]
    public async Task ListWorkspaces_ReturnsOrderedByPosition()
    {
        var betaName = U("Beta");
        var alphaName = U("Alpha");
        var create = new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp);
        var beta = await create.Handle(new CreateWorkspaceCommand(betaName, $@"C:\{betaName}"), default);
        var alpha = await create.Handle(new CreateWorkspaceCommand(alphaName, $@"C:\{alphaName}"), default);
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        var ours = result.Where(w => w.Id == beta.Id || w.Id == alpha.Id).ToList();
        ours.Should().HaveCount(2);
        ours[0].Name.Should().Be(betaName);
        ours[1].Name.Should().Be(alphaName);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsCorrectWorkspace()
    {
        var name = U("MyRepo");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\code\{name}"), default);
        var handler = new GetWorkspaceQueryHandler(_factory);

        var result = await handler.Handle(new GetWorkspaceQuery(created.Id), default);

        result.Should().NotBeNull();
        result!.Name.Should().Be(name);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsNullForUnknownId()
    {
        var handler = new GetWorkspaceQueryHandler(_factory);

        var result = await handler.Handle(new GetWorkspaceQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesNameAndPath()
    {
        var oldName = U("Old");
        var newName = U("New");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(oldName, $@"C:\{oldName}"), default);
        var handler = new UpdateWorkspaceCommandHandler(_factory);

        var result = await handler.Handle(
            new UpdateWorkspaceCommand(created.Id, newName, $@"C:\{newName}"), default);

        result.Name.Should().Be(newName);
        result.Path.Should().Be($@"C:\{newName}");
        result.UpdatedAt.Should().BeOnOrAfter(result.CreatedAt);

        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Workspaces.FindAsync(created.Id);
        persisted!.Name.Should().Be(newName);
        persisted.Path.Should().Be($@"C:\{newName}");
    }

    [Fact]
    public async Task UpdateWorkspace_NonexistentWorkspace_Throws()
    {
        var handler = new UpdateWorkspaceCommandHandler(_factory);

        var act = () => handler.Handle(
            new UpdateWorkspaceCommand(Guid.NewGuid(), U("Missing"), @"C:\missing"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteWorkspace_AfterRemove_DeletesFromDatabase()
    {
        var name = U("ToDelete");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(created.Id), default);

        await new DeleteWorkspaceCommandHandler(_factory)
            .Handle(new DeleteWorkspaceCommand(created.Id), default);

        var remaining = await new ListWorkspacesQueryHandler(_factory)
            .Handle(new ListWorkspacesQuery(IncludeRemoved: true), default);
        remaining.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task DeleteWorkspace_ActiveWorkspace_Throws()
    {
        var name = U("ActiveDel");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);

        var act = () => new DeleteWorkspaceCommandHandler(_factory)
            .Handle(new DeleteWorkspaceCommand(created.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active*");
    }

    [Fact]
    public async Task DeleteWorkspace_NonexistentWorkspace_Throws()
    {
        var handler = new DeleteWorkspaceCommandHandler(_factory);

        var act = () => handler.Handle(new DeleteWorkspaceCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task InitWorkspace_FirstRun_CreatesWorkspace()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\my-repo-{tag}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand(path, "My Repo"), default);

        result.Created.Should().BeTrue();
        result.Workspace.Name.Should().Be("My Repo");
        result.Workspace.Path.Should().Be(Path.GetFullPath(path));
    }

    [Fact]
    public async Task InitWorkspace_DefaultsNameToDirectoryName()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var dir = $"my-repo-{tag}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\{dir}"), default);

        result.Workspace.Name.Should().Be(dir);
    }

    [Fact]
    public async Task InitWorkspace_Rerun_IsNoOp()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\alpha-{tag}";
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        var result = await handler.Handle(new InitWorkspaceCommand(path, "Alpha"), default);

        result.Created.Should().BeFalse();
        var workspaceCount = await _db.Workspaces.CountAsync(w => w.Path == result.Workspace.Path);
        workspaceCount.Should().Be(1);
    }

    [Fact]
    public async Task InitWorkspace_PathMatchIsCaseInsensitive()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var handler = CreateInitHandler();
        await handler.Handle(new InitWorkspaceCommand($@"C:\Projects\Repo-{tag}", "Repo"), default);

        var result = await handler.Handle(new InitWorkspaceCommand($@"C:\projects\repo-{tag}"), default);

        result.Created.Should().BeFalse();
    }

    [Fact]
    public async Task InitWorkspace_NullPath_Throws()
    {
        var handler = CreateInitHandler();

        var act = () => handler.Handle(new InitWorkspaceCommand(null!), default);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InitWorkspace_EmptyPath_Throws()
    {
        var handler = CreateInitHandler();

        var act = () => handler.Handle(new InitWorkspaceCommand(""), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task InitWorkspace_NonExistentDirectory_CreatesWorkspaceWithDirectoryName()
    {
        var dirName = $"ghost-{Guid.NewGuid():N}"[..20];
        var path = $@"C:\does-not-exist\{dirName}";
        var handler = CreateInitHandler();

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.Created.Should().BeTrue();
        result.Workspace.Name.Should().Be(dirName);
    }

    [Fact]
    public async Task InitWorkspace_SaveChangesThrows_PropagatesException()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var connStr = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        await using var conn = new SqliteConnection(connStr);
        conn.Open();
        var baseOpts = new DbContextOptionsBuilder<BishopDbContext>().UseSqlite(connStr).Options;
        await using (var schemaCtx = new BishopDbContext(baseOpts))
            schemaCtx.Database.EnsureCreated();

        var throwOpts = new DbContextOptionsBuilder<BishopDbContext>()
            .UseSqlite(connStr)
            .AddInterceptors(new ThrowingSaveChangesInterceptor())
            .Options;
        var handler = new InitWorkspaceCommandHandler(new DirectDbContextFactory(throwOpts));

        var act = () => handler.Handle(new InitWorkspaceCommand(@"C:\new-ws-save-fail"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated DB failure");
    }

    [Fact]
    public async Task InitWorkspace_ArchivedAtPath_NoAction_ReturnsNeedsArchivedAction()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\archived-{tag}";
        var handler = CreateInitHandler();
        var first = await handler.Handle(new InitWorkspaceCommand(path, "Archived"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System).Handle(new RemoveWorkspaceCommand(first.Workspace.Id), default);

        var result = await handler.Handle(new InitWorkspaceCommand(path), default);

        result.NeedsArchivedAction.Should().BeTrue();
        result.Workspace.Name.Should().Be("Archived");
        result.Created.Should().BeFalse();
        result.Restored.Should().BeFalse();
    }

    [Fact]
    public async Task InitWorkspace_ArchivedAtPath_Restore_ReactivatesWorkspaceWithHistory()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\restore-{tag}";
        var handler = CreateInitHandler();
        var first = await handler.Handle(new InitWorkspaceCommand(path, "ToRestore"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System).Handle(new RemoveWorkspaceCommand(first.Workspace.Id), default);

        var result = await handler.Handle(new InitWorkspaceCommand(path, ArchivedAction: InitWorkspaceArchivedAction.Restore), default);

        result.Restored.Should().BeTrue();
        result.Created.Should().BeFalse();
        result.NeedsArchivedAction.Should().BeFalse();
        result.Workspace.Id.Should().Be(first.Workspace.Id);
        result.Workspace.Name.Should().Be("ToRestore");
        var ws = await _db.Workspaces.FindAsync(first.Workspace.Id);
        ws!.IsRemoved.Should().BeFalse();
        ws.RemovedAt.Should().BeNull();
    }

    [Fact]
    public async Task InitWorkspace_ArchivedAtPath_Fresh_PurgesAndCreatesNewWorkspace()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var path = $@"C:\projects\fresh-{tag}";
        var handler = CreateInitHandler();
        var first = await handler.Handle(new InitWorkspaceCommand(path, "OldWs"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System).Handle(new RemoveWorkspaceCommand(first.Workspace.Id), default);

        var result = await handler.Handle(new InitWorkspaceCommand(path, "NewWs", ArchivedAction: InitWorkspaceArchivedAction.Fresh), default);

        result.Created.Should().BeTrue();
        result.Restored.Should().BeFalse();
        result.NeedsArchivedAction.Should().BeFalse();
        result.Workspace.Id.Should().NotBe(first.Workspace.Id);
        result.Workspace.Name.Should().Be("NewWs");
        var old = await _db.Workspaces.FindAsync(first.Workspace.Id);
        old.Should().BeNull();
    }

    [Fact]
    public async Task ReorderWorkspaces_UpdatesPositionsInOrder()
    {
        var create = new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("First"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Second"), $@"C:\{U()}"), default);
        var handler = new ReorderWorkspacesCommandHandler(_factory);

        await handler.Handle(new ReorderWorkspacesCommand([ws2.Id, ws1.Id]), default);

        var updated1 = (await _db.Workspaces.FindAsync(ws1.Id))!;
        var updated2 = (await _db.Workspaces.FindAsync(ws2.Id))!;
        updated2.Position.Should().Be(1);
        updated1.Position.Should().Be(2);
    }

    [Fact]
    public async Task ReorderWorkspaces_WorkspaceNotInList_KeepsItsPosition()
    {
        var create = new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp);
        var ws1 = await create.Handle(new CreateWorkspaceCommand(U("Keep"), $@"C:\{U()}"), default);
        var ws2 = await create.Handle(new CreateWorkspaceCommand(U("Move"), $@"C:\{U()}"), default);
        var originalWs1Position = ws1.Position;
        var handler = new ReorderWorkspacesCommandHandler(_factory);

        await handler.Handle(new ReorderWorkspacesCommand([ws2.Id]), default);

        var updated1 = (await _db.Workspaces.FindAsync(ws1.Id))!;
        updated1.Position.Should().Be(originalWs1Position);
    }

    [Fact]
    public async Task LaunchPlainTerminal_ReturnsTrue_WhenLauncherSucceeds()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchPlain(Arg.Any<string>(), Arg.Any<TerminalSnap?>()).Returns(true);
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        var result = await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace"), default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchPlainTerminal_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.LaunchPlain(Arg.Any<string>(), Arg.Any<TerminalSnap?>()).Returns(false);
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        var result = await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace"), default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchPlainTerminal_ForwardsPathAndSnapToLauncher()
    {
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchPlainTerminalCommandHandler(launcher);

        await handler.Handle(new LaunchPlainTerminalCommand(@"C:\workspace", snap), default);

        launcher.Received(1).LaunchPlain(@"C:\workspace", snap);
    }

    [Fact]
    public async Task LaunchWorkspace_ReturnsTrue_WhenLauncherSucceeds()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(true);
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        var result = await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task LaunchWorkspace_ReturnsFalse_WhenLauncherReturnsFalse()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        launcher.Launch(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<TerminalSnap?>(), Arg.Any<string?>()).Returns(false);
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        var result = await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task LaunchWorkspace_PassesNullClaudeArgsAndForwardsSnap()
    {
        var snap = new TerminalSnap(0, 0, 800, 600);
        var launcher = Substitute.For<ITerminalLauncher>();
        var handler = new LaunchWorkspaceCommandHandler(launcher, Substitute.For<IWorkspaceContextSeeder>());

        await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace", snap), default);

        launcher.Received(1).Launch(@"C:\workspace", null, snap);
    }

    [Fact]
    public async Task LaunchWorkspace_CallsSeederBeforeLaunch()
    {
        var launcher = Substitute.For<ITerminalLauncher>();
        var seeder = Substitute.For<IWorkspaceContextSeeder>();
        var handler = new LaunchWorkspaceCommandHandler(launcher, seeder);

        await handler.Handle(new LaunchWorkspaceCommand(@"C:\workspace"), default);

        await seeder.Received(1).SeedAsync(@"C:\workspace", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveWorkspace_SetsIsRemovedAndRemovedAt()
    {
        var name = U("ToRemove");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System);

        await handler.Handle(new RemoveWorkspaceCommand(created.Id), default);

        var ws = await _db.Workspaces.FindAsync(created.Id);
        ws!.IsRemoved.Should().BeTrue();
        ws.RemovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveWorkspace_WorkspaceNotFound_Throws()
    {
        var handler = new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System);

        var act = () => handler.Handle(new RemoveWorkspaceCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RemoveWorkspace_RemovedWorkspaceExcludedFromList()
    {
        var name = U("Removed2");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(created.Id), default);

        var result = await new ListWorkspacesQueryHandler(_factory)
            .Handle(new ListWorkspacesQuery(), default);

        result.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task ListWorkspaces_ExcludesRemovedWorkspaces()
    {
        var name = U("Removed");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var ws = await _db.Workspaces.FindAsync(created.Id);
        ws!.IsRemoved = true;
        ws.RemovedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        result.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task ListWorkspaces_IncludesNonRemovedWorkspaces()
    {
        var name = U("Active");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(), default);

        result.Should().Contain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task ListWorkspaces_IncludeRemoved_IncludesRemovedWorkspace()
    {
        var name = U("RemovedIncl");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var ws = await _db.Workspaces.FindAsync(created.Id);
        ws!.IsRemoved = true;
        ws.RemovedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var handler = new ListWorkspacesQueryHandler(_factory);

        var result = await handler.Handle(new ListWorkspacesQuery(IncludeRemoved: true), default);

        result.Should().Contain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task PurgeWorkspace_RemovedWorkspace_DeletesFromDb()
    {
        var name = U("ToPurge");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(created.Id), default);
        var handler = new PurgeWorkspaceCommandHandler(_factory);

        await handler.Handle(new PurgeWorkspaceCommand(created.Id), default);

        var ws = await _db.Workspaces.FindAsync(created.Id);
        ws.Should().BeNull();
    }

    [Fact]
    public async Task PurgeWorkspace_ActiveWorkspace_Throws()
    {
        var name = U("ActivePurge");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var handler = new PurgeWorkspaceCommandHandler(_factory);

        var act = () => handler.Handle(new PurgeWorkspaceCommand(created.Id), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active*");
    }

    [Fact]
    public async Task PurgeWorkspace_WorkspaceNotFound_Throws()
    {
        var handler = new PurgeWorkspaceCommandHandler(_factory);

        var act = () => handler.Handle(new PurgeWorkspaceCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task PurgeWorkspace_RemovedWorkspace_ExcludedFromListAfterPurge()
    {
        var name = U("PurgeList");
        var created = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(created.Id), default);
        await new PurgeWorkspaceCommandHandler(_factory)
            .Handle(new PurgeWorkspaceCommand(created.Id), default);

        var result = await new ListWorkspacesQueryHandler(_factory)
            .Handle(new ListWorkspacesQuery(IncludeRemoved: true), default);

        result.Should().NotContain(w => w.Id == created.Id);
    }

    [Fact]
    public async Task DeleteWorkspace_AfterRemove_CascadesAssociatedCards()
    {
        var name = U("DelCascade");
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Cascade test card"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(workspace.Id), default);

        await new DeleteWorkspaceCommandHandler(_factory)
            .Handle(new DeleteWorkspaceCommand(workspace.Id), default);

        var cardsExist = await _db.Cards.AnyAsync(c => c.WorkspaceId == workspace.Id);
        cardsExist.Should().BeFalse();
    }

    [Fact]
    public async Task InitWorkspace_SameNameAsRemovedWorkspace_DifferentPath_Succeeds()
    {
        var name = U("SharedName");
        var first = await CreateInitHandler()
            .Handle(new InitWorkspaceCommand($@"C:\first-{name}", name), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(first.Workspace.Id), default);

        var result = await CreateInitHandler()
            .Handle(new InitWorkspaceCommand($@"C:\second-{name}", name), default);

        result.Created.Should().BeTrue();
        result.Workspace.Name.Should().Be(name);
        result.Workspace.Id.Should().NotBe(first.Workspace.Id);
    }

    [Fact]
    public async Task PurgeWorkspace_CascadesAssociatedCards()
    {
        var name = U("PurgeCascade");
        var workspace = await new CreateWorkspaceCommandHandler(_factory, TestBootstrappers.NoOp)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        await new AddCardCommandHandler(_factory)
            .Handle(new AddCardCommand(workspace.Id, SystemLaneNames.ToDo, "Cascade test card"), default);
        await new RemoveWorkspaceCommandHandler(_factory, TimeProvider.System)
            .Handle(new RemoveWorkspaceCommand(workspace.Id), default);

        await new PurgeWorkspaceCommandHandler(_factory)
            .Handle(new PurgeWorkspaceCommand(workspace.Id), default);

        var cardsExist = await _db.Cards.AnyAsync(c => c.WorkspaceId == workspace.Id);
        cardsExist.Should().BeFalse();
    }

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated DB failure");
    }

    private sealed class DirectDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly DbContextOptions<BishopDbContext> _options;
        public DirectDbContextFactory(DbContextOptions<BishopDbContext> options) => _options = options;
        public BishopDbContext CreateDbContext() => new(_options);
    }
}
