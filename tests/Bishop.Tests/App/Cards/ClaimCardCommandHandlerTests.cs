using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.ClaimCard;
using Bishop.App.Cards.GetCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Bishop.Tests.App.Cards;

public sealed class ClaimCardCommandHandlerTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;
    private readonly IDbContextFactory<BishopDbContext> _factory;
    private readonly SqliteConnection _connection;

    public ClaimCardCommandHandlerTests(DbFixture fixture)
    {
        _db = fixture.Db;
        _factory = fixture.Factory;
        _connection = fixture.Connection;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace workspace, IReadOnlyList<LaneInfo> lanes)> CreateWorkspaceWithLanesAsync()
    {
        var name = U("Test");
        var workspace = await new CreateWorkspaceCommandHandler(_factory)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler()
            .Handle(new ListLanesByWorkspaceQuery(workspace.Id), default);
        return (workspace, lanes);
    }

    private ISender CreateSender()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.ArgAt<GetCardQuery>(0);
                using var db = _factory.CreateDbContext();
                return Task.FromResult<Card?>(db.Cards.Find(query.CardId));
            });
        return sender;
    }

    [Fact]
    public async Task Claim_WithoutTag_PicksTopOfSourceLaneAndMovesToDoing()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var doing = lanes.Single(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"First"), default);
        var second = await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Second"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act — Second was added last so insert-at-top puts it at position 1
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(second.Id);
        claimed.LaneName.Should().Be(doing.Name);
    }

    [Fact]
    public async Task Claim_WithTag_PicksFirstMatchingCardEvenWhenNotAtTop()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var doing = lanes.Single(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_factory);

        // Inserted in this order — with insert-at-top, final lane order top→bottom is:
        // Plain-3 (pos 1), Tagged-test (pos 2), Plain-1 (pos 3)
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Plain-1"), default);
        var tagged = await add.Handle(
            new AddCardCommand(workspace.Id, todo.Name,"Tagged-test", TagName: "test"),
            default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Plain-3"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do", TagName: "test"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(tagged.Id);
        claimed.LaneName.Should().Be(doing.Name);
        claimed.TagName.Should().Be("test");
    }

    [Fact]
    public async Task Claim_WithTag_NoMatchButOtherCardsPresent_ReturnsNullAndMovesNothing()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var doing = lanes.Single(l => l.Name == "Doing");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Plain-1"), default);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name,"Tagged-bug", TagName: "bug"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do", TagName: "test"),
            default);

        // Assert
        claimed.Should().BeNull();
        var todoCount = await _db.Cards.CountAsync(c => c.WorkspaceId == workspace.Id && c.LaneName == todo.Name);
        var doingCount = await _db.Cards.CountAsync(c => c.WorkspaceId == workspace.Id && c.LaneName == doing.Name);
        todoCount.Should().Be(2);
        doingCount.Should().Be(0);
    }

    [Fact]
    public async Task Claim_EmptySourceLane_ReturnsNull()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert
        claimed.Should().BeNull();
    }

    [Fact]
    public async Task Claim_EmptySourceLane_WithTag_ReturnsNull()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do", TagName: "test"),
            default);

        // Assert
        claimed.Should().BeNull();
    }

    [Fact]
    public async Task Claim_UnknownSourceLane_Throws()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var act = async () => await handler.Handle(
            new ClaimCardCommand(workspace.Id, "Nope"),
            default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Lane 'Nope' is not a system lane.");
    }

    [Fact]
    public async Task Claim_SqliteBusyOnce_RetriesAndSucceeds()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "Only card"), default);
        var faultingFactory = new FaultingDbContextFactory(_factory, throwCount: 1);
        var handler = new ClaimCardCommandHandler(faultingFactory, CreateSender());

        // Act — the first attempt throws SQLITE_BUSY (code 5); the retry succeeds
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(card.Id);
    }

    [Fact]
    public async Task Claim_CaseInsensitiveLaneName_MatchesCard()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, "To Do", "Test card"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act — "TO DO" should match the "To Do" lane via OrdinalIgnoreCase comparison
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "TO DO"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(card.Id);
        claimed.LaneName.Should().Be("Doing");
    }

    [Fact]
    public async Task Claim_SqliteBusyTwice_RetriesAndSucceedsOnThirdAttempt()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        var card = await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "Only card"), default);
        var faultingFactory = new FaultingDbContextFactory(_factory, throwCount: 2);
        var handler = new ClaimCardCommandHandler(faultingFactory, CreateSender());

        // Act — busy on attempts 1 and 2; attempt 3 succeeds
        var claimed = await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(card.Id);
        faultingFactory.CreateCallCount.Should().Be(3);
    }

    [Fact]
    public async Task Claim_SqliteBusyOnEveryAttempt_PropagatesAfterMaxAttempts()
    {
        // Arrange
        var (workspace, lanes) = await CreateWorkspaceWithLanesAsync();
        var todo = lanes.Single(l => l.Name == "To Do");
        var add = new AddCardCommandHandler(_factory);
        await add.Handle(new AddCardCommand(workspace.Id, todo.Name, "Only card"), default);
        var faultingFactory = new FaultingDbContextFactory(_factory, throwCount: 99);
        var handler = new ClaimCardCommandHandler(faultingFactory, CreateSender());

        // Act — every attempt throws SQLITE_BUSY; the catch filter (attempt < MaxAttempts)
        // rejects on the final attempt, so the last exception propagates rather than being swallowed.
        var act = async () => await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert — exception surfaces on attempt 3; the post-loop `return null` is unreachable
        // for the retryable-on-every-attempt case, which boundary-checks the for + catch conditions.
        await act.Should().ThrowAsync<SqliteException>();
        faultingFactory.CreateCallCount.Should().Be(3);
    }

    [Fact]
    public async Task Claim_NonSqliteException_PropagatesWithoutRetry()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var faultingFactory = new FaultingDbContextFactory(
            _factory,
            throwCount: 99,
            new InvalidOperationException("boom"));
        var handler = new ClaimCardCommandHandler(faultingFactory, CreateSender());

        // Act
        var act = async () => await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert — non-Sqlite exception is not retryable: surface on first attempt
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        faultingFactory.CreateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Claim_NonBusySqliteException_PropagatesWithoutRetry()
    {
        // Arrange
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        // SqliteException with a non-busy/locked error code (1 = SQLITE_ERROR)
        var faultingFactory = new FaultingDbContextFactory(
            _factory,
            throwCount: 99,
            new SqliteException("generic error", 1));
        var handler = new ClaimCardCommandHandler(faultingFactory, CreateSender());

        // Act
        var act = async () => await handler.Handle(
            new ClaimCardCommand(workspace.Id, "To Do"),
            default);

        // Assert — non-retryable Sqlite code: surface on first attempt
        await act.Should().ThrowAsync<SqliteException>();
        faultingFactory.CreateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Claim_OnlyAffectsSourceLaneInSameWorkspace()
    {
        // Arrange — workspace A is the claim target; workspace B and lane "Backlog" must be untouched
        var (wsA, _) = await CreateWorkspaceWithLanesAsync();
        var (wsB, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);

        await add.Handle(new AddCardCommand(wsA.Id, "To Do", "A-todo-1"), default);
        var aTodoTop = await add.Handle(new AddCardCommand(wsA.Id, "To Do", "A-todo-2"), default);
        var aBacklog = await add.Handle(new AddCardCommand(wsA.Id, "Backlog", "A-backlog"), default);
        var bTodo = await add.Handle(new AddCardCommand(wsB.Id, "To Do", "B-todo"), default);

        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(new ClaimCardCommand(wsA.Id, "To Do"), default);

        // Assert — only workspace A's To Do is touched
        claimed.Should().NotBeNull();
        claimed!.Id.Should().Be(aTodoTop.Id);

        using var verify = _factory.CreateDbContext();
        (await verify.Cards.FindAsync(aBacklog.Id))!.LaneName.Should().Be("Backlog");
        (await verify.Cards.FindAsync(bTodo.Id))!.LaneName.Should().Be("To Do");
    }

    [Fact]
    public async Task Claim_SourceSiblings_ReorderedSequentiallyFromOnePreservingOriginalOrder()
    {
        // Arrange — insert-at-top order: top→bottom after all adds is D(1), C(2), B(3), A(4)
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        var a = await add.Handle(new AddCardCommand(workspace.Id, "To Do", "A"), default);
        var b = await add.Handle(new AddCardCommand(workspace.Id, "To Do", "B"), default);
        var c = await add.Handle(new AddCardCommand(workspace.Id, "To Do", "C"), default);
        var d = await add.Handle(new AddCardCommand(workspace.Id, "To Do", "D"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act — D is the top card
        var claimed = await handler.Handle(new ClaimCardCommand(workspace.Id, "To Do"), default);
        claimed!.Id.Should().Be(d.Id);

        // Assert — remaining cards keep their relative order: C(1), B(2), A(3)
        using var verify = _factory.CreateDbContext();
        var remaining = await verify.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == "To Do")
            .OrderBy(x => x.Position)
            .Select(x => new { x.Id, x.Position })
            .ToListAsync();
        remaining.Should().HaveCount(3);
        remaining[0].Id.Should().Be(c.Id); remaining[0].Position.Should().Be(1);
        remaining[1].Id.Should().Be(b.Id); remaining[1].Position.Should().Be(2);
        remaining[2].Id.Should().Be(a.Id); remaining[2].Position.Should().Be(3);
    }

    [Fact]
    public async Task Claim_DoingLane_ClaimedCardInsertedAtTopAndSiblingsRenumbered()
    {
        // Arrange — seed two existing Doing cards, then one To Do card to claim
        var (workspace, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        var doing1 = await add.Handle(new AddCardCommand(workspace.Id, "Doing", "D1"), default);
        var doing2 = await add.Handle(new AddCardCommand(workspace.Id, "Doing", "D2"), default);
        var todo = await add.Handle(new AddCardCommand(workspace.Id, "To Do", "T"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(new ClaimCardCommand(workspace.Id, "To Do"), default);

        // Assert — claimed card sits at position 1; existing Doing cards shift to 2 and 3
        claimed.Should().NotBeNull();
        using var verify = _factory.CreateDbContext();
        var doingCards = await verify.Cards
            .Where(x => x.WorkspaceId == workspace.Id && x.LaneName == "Doing")
            .OrderBy(x => x.Position)
            .Select(x => new { x.Id, x.Position })
            .ToListAsync();
        doingCards.Should().HaveCount(3);
        doingCards[0].Id.Should().Be(todo.Id);
        doingCards[0].Position.Should().Be(1);
        doingCards[1].Position.Should().Be(2);
        doingCards[2].Position.Should().Be(3);
        // Both pre-existing Doing cards present, just renumbered
        doingCards.Select(x => x.Id).Should().Contain(new[] { doing1.Id, doing2.Id });
    }

    [Fact]
    public async Task Claim_DoingLaneRenumber_OnlyAffectsSameWorkspace()
    {
        // Arrange — workspace A has an existing Doing card; workspace B is the claim target
        var (wsA, _) = await CreateWorkspaceWithLanesAsync();
        var (wsB, _) = await CreateWorkspaceWithLanesAsync();
        var add = new AddCardCommandHandler(_factory);
        var aDoing = await add.Handle(new AddCardCommand(wsA.Id, "Doing", "A-doing"), default);
        await add.Handle(new AddCardCommand(wsB.Id, "To Do", "B-todo"), default);
        var handler = new ClaimCardCommandHandler(_factory, CreateSender());

        // Act
        var claimed = await handler.Handle(new ClaimCardCommand(wsB.Id, "To Do"), default);

        // Assert — workspace A's Doing card is untouched (same lane name, different workspace)
        claimed.Should().NotBeNull();
        using var verify = _factory.CreateDbContext();
        var aDoingAfter = await verify.Cards.FindAsync(aDoing.Id);
        aDoingAfter!.LaneName.Should().Be("Doing");
        aDoingAfter.Position.Should().Be(1);
    }

    private sealed class FaultingDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly IDbContextFactory<BishopDbContext> _inner;
        private readonly int _throwCount;
        private readonly Exception _exception;
        private int _thrown;

        public int CreateCallCount { get; private set; }

        public FaultingDbContextFactory(IDbContextFactory<BishopDbContext> inner, int throwCount)
            : this(inner, throwCount, new SqliteException("SQLITE_BUSY", 5))
        {
        }

        public FaultingDbContextFactory(IDbContextFactory<BishopDbContext> inner, int throwCount, Exception exception)
        {
            _inner = inner;
            _throwCount = throwCount;
            _exception = exception;
        }

        public BishopDbContext CreateDbContext()
        {
            CreateCallCount++;
            if (_thrown < _throwCount)
            {
                _thrown++;
                throw _exception;
            }
            return _inner.CreateDbContext();
        }
    }
}
