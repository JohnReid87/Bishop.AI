using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.UpdateCard;
using Bishop.App.Lanes.ListLanesByWorkspace;
using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Core;
using Bishop.Data;
using FluentAssertions;

namespace Bishop.Tests.App.Cards;

public sealed class CardStdinTests : IClassFixture<DbFixture>
{
    // Characters that Windows-1252 / default code page cannot round-trip correctly:
    // em-dash, en-dash, curly quotes, ellipsis, Latin-1 accented char, CJK.
    private const string UnicodeBody =
        "em-dash —, en-dash –, " +
        "curly quotes ‘ ’ “ ”, " +
        "ellipsis …, accented é, CJK 漢";

    private readonly BishopDbContext _db;

    public CardStdinTests(DbFixture fixture) => _db = fixture.Db;

    private static string U(string prefix = "ws") => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(Workspace, Lane)> CreateWorkspaceWithTodoLaneAsync()
    {
        var name = U("Enc");
        var ws = await new CreateWorkspaceCommandHandler(_db)
            .Handle(new CreateWorkspaceCommand(name, $@"C:\{name}"), default);
        var lanes = await new ListLanesByWorkspaceQueryHandler(_db)
            .Handle(new ListLanesByWorkspaceQuery(ws.Id), default);
        return (ws, lanes[0]);
    }

    [Fact]
    public async Task CardAdd_StdinPath_PreservesUnicodeDescription()
    {
        var (_, lane) = await CreateWorkspaceWithTodoLaneAsync();

        var previousIn = Console.In;
        try
        {
            Console.SetIn(new StringReader(UnicodeBody));
            var desc = await Console.In.ReadToEndAsync();

            var card = await new AddCardCommandHandler(_db)
                .Handle(new AddCardCommand(lane.Id, "encoding-test-add", desc), default);

            card.Description.Should().Be(UnicodeBody);
        }
        finally
        {
            Console.SetIn(previousIn);
        }
    }

    [Fact]
    public async Task CardEdit_StdinPath_PreservesUnicodeDescription()
    {
        var (_, lane) = await CreateWorkspaceWithTodoLaneAsync();

        // Create the card with a placeholder description first.
        var card = await new AddCardCommandHandler(_db)
            .Handle(new AddCardCommand(lane.Id, "encoding-test-edit", "placeholder"), default);

        var previousIn = Console.In;
        try
        {
            Console.SetIn(new StringReader(UnicodeBody));
            var desc = await Console.In.ReadToEndAsync();

            var updated = await new UpdateCardCommandHandler(_db)
                .Handle(new UpdateCardCommand(card.Id, null, desc, false, []), default);

            updated.Description.Should().Be(UnicodeBody);
        }
        finally
        {
            Console.SetIn(previousIn);
        }
    }
}
