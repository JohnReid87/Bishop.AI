using Bishop.App.Cards.ImportFromGitHub;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Cli.Cards.ImportFromGitHub;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.CommandLine;
using System.Text.Json;

namespace Bishop.Tests.Cli.Cards.ImportFromGitHub;

[Collection("ConsoleTests")]
public sealed class ImportFromGitHubCliCommandTests
{
    private static (IMediator mediator, ImportFromGitHubCliCommand cmd) Build(
        Workspace ws, ImportFromGitHubResult result)
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Workspace>)[ws]);
        mediator.Send(Arg.Any<ImportFromGitHubCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return (mediator, new ImportFromGitHubCliCommand(mediator));
    }

    private static Workspace DefaultWorkspace() =>
        new() { Id = Guid.NewGuid(), Name = "test-ws", Path = @"C:\test" };

    [Fact]
    public async Task InvokeAsync_EmptyResult_ExitsZeroAndPrintsZeroSummary()
    {
        var ws = DefaultWorkspace();
        var (mediator, cmd) = Build(ws, new ImportFromGitHubResult([], [], []));

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Imported 0, skipped 0 (already present), failed 0.");
        await mediator.Received(1).Send(Arg.Any<ImportFromGitHubCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_PopulatedResult_PrintsImportedSkippedAndFailedLines()
    {
        var ws = DefaultWorkspace();
        var importedCard = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Title = "Fix login", GitHubIssueNumber = 42
        };
        var result = new ImportFromGitHubResult(
            [importedCard], [7], [new ImportFailure(99, "API error")]);
        var (_, cmd) = Build(ws, result);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("Imported 1, skipped 1 (already present), failed 1.");
        text.Should().Contain("imported  #42  Fix login");
        text.Should().Contain("skipped   #7");
        text.Should().Contain("failed    #99  API error");
    }

    [Fact]
    public async Task InvokeAsync_DryRun_PrintsDryRunPrefixAndWouldImportLines()
    {
        var ws = DefaultWorkspace();
        var importedCard = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Title = "Fix login", GitHubIssueNumber = 42
        };
        var result = new ImportFromGitHubResult([importedCard], [7], []);
        var (_, cmd) = Build(ws, result);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try { await cmd.InvokeAsync(["--workspace", "test-ws", "--dry-run"]); }
        finally { Console.SetOut(originalOut); }

        var text = output.ToString();
        text.Should().Contain("[dry-run] Imported 1, skipped 1 (already present), failed 0.");
        text.Should().Contain("would import  #42  Fix login");
        text.Should().Contain("would skip   #7");
    }

    [Fact]
    public async Task InvokeAsync_JsonFlag_OutputsValidJsonWithImportedSkippedFailedKeys()
    {
        var ws = DefaultWorkspace();
        var importedCard = new Card
        {
            Id = Guid.NewGuid(), WorkspaceId = ws.Id,
            Title = "Fix login", GitHubIssueNumber = 42
        };
        var result = new ImportFromGitHubResult([importedCard], [], []);
        var (_, cmd) = Build(ws, result);

        var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        int exitCode;
        try { exitCode = await cmd.InvokeAsync(["--workspace", "test-ws", "--json"]); }
        finally { Console.SetOut(originalOut); }

        exitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(output.ToString());
        var root = doc.RootElement;
        root.TryGetProperty("Imported", out _).Should().BeTrue();
        root.TryGetProperty("SkippedAlreadyPresent", out _).Should().BeTrue();
        root.TryGetProperty("Failed", out _).Should().BeTrue();
        root.GetProperty("Imported").GetArrayLength().Should().Be(1);
    }
}
