using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Core;
using Bishop.ViewModels.Batches;
using Bishop.ViewModels.Cards;
using Bishop.ViewModels.Errors;
using Bishop.ViewModels.GitHub;
using Bishop.ViewModels.Scripts;
using Bishop.ViewModels.Settings;
using Bishop.ViewModels.Shared;
using Bishop.ViewModels.Skills;
using Bishop.ViewModels.Workspaces;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.ViewModels;

public class ReportViewerWindowViewModelTests
{
    private const string ConvertPayload = """
        {"type":"convertToCard","skill":"bish-arch","title":"Title","body":"Body","severity":"high","location":"src/X.cs:42"}
        """;

    [Fact]
    public void ResolveWorkspacePathFromSource_FindsAncestorWithBishopDir()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "bishop-rvm-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(tempRoot, "ws");
        var findingsDir = Path.Combine(workspace, ".bishop", "findings");
        Directory.CreateDirectory(findingsDir);
        try
        {
            var htmlPath = Path.Combine(findingsDir, "bish-arch.html");
            File.WriteAllText(htmlPath, "<html></html>");

            var result = ReportViewerWindowViewModel.ResolveWorkspacePathFromSource(new Uri(htmlPath));

            result.Should().NotBeNull();
            result!.TrimEnd(Path.DirectorySeparatorChar)
                .Should().Be(workspace.TrimEnd(Path.DirectorySeparatorChar));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveWorkspacePathFromSource_ReturnsNullWhenNoAncestorMatches()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "bishop-rvm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var htmlPath = Path.Combine(tempRoot, "x.html");
            File.WriteAllText(htmlPath, "<html></html>");

            ReportViewerWindowViewModel.ResolveWorkspacePathFromSource(new Uri(htmlPath))
                .Should().BeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveWorkspacePathFromSource_ReturnsNullForNullOrNonFileUri()
    {
        ReportViewerWindowViewModel.ResolveWorkspacePathFromSource(null).Should().BeNull();
        ReportViewerWindowViewModel.ResolveWorkspacePathFromSource(new Uri("https://example.com/x.html"))
            .Should().BeNull();
    }

    [Fact]
    public void ParsePayload_ReturnsNullForNonConvertMessage()
    {
        ReportViewerWindowViewModel.ParsePayload("""{"type":"other","title":"x"}""").Should().BeNull();
        ReportViewerWindowViewModel.ParsePayload("not json").Should().BeNull();
        ReportViewerWindowViewModel.ParsePayload("").Should().BeNull();
    }

    [Fact]
    public async Task HandleConvertToCardAsync_DismissedDialog_RemovesDraft()
    {
        var (tempRoot, workspace, htmlPath) = CreateTempWorkspace();
        try
        {
            var ws = new Workspace { Id = Guid.NewGuid(), Path = workspace, Name = "ws" };
            var newCard = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Title = "Title", LaneName = SystemLaneNames.ToDo };

            var mediator = Substitute.For<ISender>();
            mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<Workspace>)[ws]);
            mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
                .Returns(newCard);
            mediator.Send(Arg.Any<RemoveCardCommand>(), Arg.Any<CancellationToken>())
                .Returns(Unit.Value);

            var dialog = Substitute.For<ICardDetailDialogService>();
            dialog.ShowAsync(Arg.Any<CardViewModel>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<object>())
                .Returns(false);

            var vm = new ReportViewerWindowViewModel(mediator, dialog, new SkillTagMap());

            await vm.HandleConvertToCardAsync(ConvertPayload, new Uri(htmlPath), new object());

            await mediator.Received(1).Send(
                Arg.Is<AddCardCommand>(c =>
                    c.WorkspaceId == ws.Id &&
                    c.LaneName == SystemLaneNames.ToDo &&
                    c.Title == "Title" &&
                    c.TagName == "arch" &&
                    c.Description.Contains("### Why") &&
                    c.Description.Contains("Body") &&
                    c.Description.Contains("bish-arch")),
                Arg.Any<CancellationToken>());
            await mediator.Received(1).Send(
                Arg.Is<RemoveCardCommand>(c => c.CardId == newCard.Id),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task HandleConvertToCardAsync_SavedDialog_DoesNotRemove()
    {
        var (tempRoot, workspace, htmlPath) = CreateTempWorkspace();
        try
        {
            var ws = new Workspace { Id = Guid.NewGuid(), Path = workspace, Name = "ws" };
            var newCard = new Card { Id = Guid.NewGuid(), WorkspaceId = ws.Id, Title = "Title", LaneName = SystemLaneNames.ToDo };

            var mediator = Substitute.For<ISender>();
            mediator.Send(Arg.Any<ListWorkspacesQuery>(), Arg.Any<CancellationToken>())
                .Returns((IReadOnlyList<Workspace>)[ws]);
            mediator.Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>())
                .Returns(newCard);

            var dialog = Substitute.For<ICardDetailDialogService>();
            dialog.ShowAsync(Arg.Any<CardViewModel>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<object>())
                .Returns(true);

            var vm = new ReportViewerWindowViewModel(mediator, dialog, new SkillTagMap());

            await vm.HandleConvertToCardAsync(ConvertPayload, new Uri(htmlPath), new object());

            await mediator.DidNotReceive().Send(Arg.Any<RemoveCardCommand>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task HandleConvertToCardAsync_NoWorkspaceMatch_DoesNothing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "bishop-rvm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var htmlPath = Path.Combine(tempRoot, "x.html");
            File.WriteAllText(htmlPath, "<html></html>");

            var mediator = Substitute.For<ISender>();
            var dialog = Substitute.For<ICardDetailDialogService>();
            var vm = new ReportViewerWindowViewModel(mediator, dialog, new SkillTagMap());

            await vm.HandleConvertToCardAsync(ConvertPayload, new Uri(htmlPath), new object());

            await mediator.DidNotReceive().Send(Arg.Any<AddCardCommand>(), Arg.Any<CancellationToken>());
            await dialog.DidNotReceive().ShowAsync(
                Arg.Any<CardViewModel>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<object>());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static (string tempRoot, string workspace, string htmlPath) CreateTempWorkspace()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "bishop-rvm-" + Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(tempRoot, "ws");
        var findingsDir = Path.Combine(workspace, ".bishop", "findings");
        Directory.CreateDirectory(findingsDir);
        var htmlPath = Path.Combine(findingsDir, "bish-arch.html");
        File.WriteAllText(htmlPath, "<html></html>");
        return (tempRoot, workspace, htmlPath);
    }
}
