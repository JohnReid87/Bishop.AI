using Bishop.App.Tags;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Tags;

public sealed class DefaultTagSeederTests : IClassFixture<DbFixture>
{
    private readonly BishopDbContext _db;

    public DefaultTagSeederTests(DbFixture fixture)
    {
        _db = fixture.Db;
    }

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<Workspace> CreateWorkspaceAsync(string path)
    {
        return await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(U(), path), default);
    }

    [Fact]
    public async Task EnsureAsync_EmptyWorkspace_InsertsAllSevenBrandTags()
    {
        // Arrange
        var path = $@"C:\projects\seed-{U()}";
        var workspace = await CreateWorkspaceAsync(path);
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAsync(path, default);

        // Assert
        var tags = await _db.Tags
            .Where(t => t.WorkspaceId == workspace.Id)
            .ToDictionaryAsync(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);
        tags.Should().HaveCount(BrandTagPalette.DefaultColours.Count);
        foreach (var (name, expected) in BrandTagPalette.DefaultColours)
            tags[name].Should().Be(expected);
    }

    [Fact]
    public async Task EnsureAsync_StaleGreyColours_UpdatesToBrandGreens()
    {
        // Arrange — simulate the pre-card state: built-in tags exist with the
        // old default grey or the legacy docs blue.
        var path = $@"C:\projects\stale-{U()}";
        var workspace = await CreateWorkspaceAsync(path);
        foreach (var name in TagNames.All)
        {
            _db.Tags.Add(new Tag
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = name,
                Colour = name == "docs" ? "#4A90D9" : "#888888",
            });
        }
        await _db.SaveChangesAsync();
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAsync(path, default);

        // Assert
        var tags = await _db.Tags
            .Where(t => t.WorkspaceId == workspace.Id)
            .ToDictionaryAsync(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, expected) in BrandTagPalette.DefaultColours)
            tags[name].Should().Be(expected);
    }

    [Fact]
    public async Task EnsureAsync_CustomUserTag_IsPreserved()
    {
        // Arrange
        var path = $@"C:\projects\custom-{U()}";
        var workspace = await CreateWorkspaceAsync(path);
        _db.Tags.Add(new Tag
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "blocked",
            Colour = "#ff5555",
        });
        await _db.SaveChangesAsync();
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAsync(path, default);

        // Assert
        var custom = await _db.Tags
            .SingleAsync(t => t.WorkspaceId == workspace.Id && t.Name == "blocked");
        custom.Colour.Should().Be("#ff5555");
    }

    [Fact]
    public async Task EnsureAsync_PartialOverlap_FillsMissingAndUpdatesExisting()
    {
        // Arrange — only `arch` is present, with the wrong colour.
        var path = $@"C:\projects\partial-{U()}";
        var workspace = await CreateWorkspaceAsync(path);
        _db.Tags.Add(new Tag
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Name = "arch",
            Colour = "#888888",
        });
        await _db.SaveChangesAsync();
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAsync(path, default);

        // Assert
        var tags = await _db.Tags
            .Where(t => t.WorkspaceId == workspace.Id)
            .ToDictionaryAsync(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);
        tags.Should().HaveCount(BrandTagPalette.DefaultColours.Count);
        tags["arch"].Should().Be(BrandTagPalette.DefaultColours["arch"]);
    }

    [Fact]
    public async Task EnsureAsync_AlreadySeeded_IsNoOp()
    {
        // Arrange
        var path = $@"C:\projects\noop-{U()}";
        var workspace = await CreateWorkspaceAsync(path);
        foreach (var (name, colour) in BrandTagPalette.DefaultColours)
        {
            _db.Tags.Add(new Tag
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                Name = name,
                Colour = colour,
            });
        }
        await _db.SaveChangesAsync();
        var beforeIds = await _db.Tags
            .Where(t => t.WorkspaceId == workspace.Id)
            .Select(t => t.Id)
            .ToListAsync();
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAsync(path, default);

        // Assert — no rows added or replaced.
        var afterIds = await _db.Tags
            .Where(t => t.WorkspaceId == workspace.Id)
            .Select(t => t.Id)
            .ToListAsync();
        afterIds.Should().BeEquivalentTo(beforeIds);
    }

    [Fact]
    public async Task EnsureAsync_UnknownPath_DoesNothing()
    {
        // Arrange
        var seeder = new DefaultTagSeeder(_db);
        var tagsBefore = await _db.Tags.CountAsync();

        // Act
        await seeder.EnsureAsync($@"C:\nope\{U()}", default);

        // Assert
        var tagsAfter = await _db.Tags.CountAsync();
        tagsAfter.Should().Be(tagsBefore);
    }

    [Fact]
    public async Task EnsureAllAsync_AppliesBrandGreensToEveryWorkspace()
    {
        // Arrange — two workspaces with stale built-in tag colours.
        var wsA = await CreateWorkspaceAsync($@"C:\projects\all-a-{U()}");
        var wsB = await CreateWorkspaceAsync($@"C:\projects\all-b-{U()}");
        foreach (var workspaceId in new[] { wsA.Id, wsB.Id })
        {
            foreach (var name in TagNames.All)
            {
                _db.Tags.Add(new Tag
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    Name = name,
                    Colour = "#888888",
                });
            }
        }
        await _db.SaveChangesAsync();
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAllAsync(default);

        // Assert — both workspaces now carry the brand greens.
        foreach (var workspaceId in new[] { wsA.Id, wsB.Id })
        {
            var tags = await _db.Tags
                .Where(t => t.WorkspaceId == workspaceId)
                .ToDictionaryAsync(t => t.Name, t => t.Colour, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, expected) in BrandTagPalette.DefaultColours)
                tags[name].Should().Be(expected);
        }
    }

    [Fact]
    public async Task EnsureAsync_PathMatchIsCaseInsensitive()
    {
        // Arrange
        var path = $@"C:\Projects\Case-{U()}";
        var workspace = await CreateWorkspaceAsync(path);
        var seeder = new DefaultTagSeeder(_db);

        // Act
        await seeder.EnsureAsync(path.ToLowerInvariant(), default);

        // Assert
        var count = await _db.Tags.CountAsync(t => t.WorkspaceId == workspace.Id);
        count.Should().Be(BrandTagPalette.DefaultColours.Count);
    }
}
