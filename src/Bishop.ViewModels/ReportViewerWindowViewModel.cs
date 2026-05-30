using System.Text.Json;
using Bishop.App.Cards.AddCard;
using Bishop.App.Cards.RemoveCard;
using Bishop.App.Workspaces.ListWorkspaces;
using Bishop.Core;
using MediatR;

namespace Bishop.ViewModels;

public sealed class ReportViewerWindowViewModel
{
    private readonly ISender _mediator;
    private readonly ICardDetailDialogService _dialogService;
    private readonly ISkillTagMap _skillTagMap;

    public ReportViewerWindowViewModel(
        ISender mediator,
        ICardDetailDialogService dialogService,
        ISkillTagMap skillTagMap)
    {
        _mediator = mediator;
        _dialogService = dialogService;
        _skillTagMap = skillTagMap;
    }

    /// <summary>
    /// Handle a <c>convertToCard</c> message from the findings viewer:
    /// resolve the workspace by walking <paramref name="sourceUri"/>'s ancestors
    /// for a <c>.bishop</c> folder, create a draft card pre-populated from the
    /// finding, open <c>CardDetailDialog</c>, and remove the draft on dismiss.
    /// </summary>
    public async Task HandleConvertToCardAsync(
        string payloadJson,
        Uri? sourceUri,
        object xamlRoot,
        CancellationToken cancellationToken = default)
    {
        var payload = ParsePayload(payloadJson);
        if (payload is null) return;

        var workspacePath = ResolveWorkspacePathFromSource(sourceUri);
        if (workspacePath is null) return;

        var workspaces = await _mediator.Send(new ListWorkspacesQuery(), cancellationToken);
        var workspace = workspaces.FirstOrDefault(w =>
            !string.IsNullOrEmpty(w.Path) &&
            string.Equals(
                Path.GetFullPath(w.Path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                workspacePath,
                StringComparison.OrdinalIgnoreCase));
        if (workspace is null) return;

        var description = BuildDescription(payload);
        var tag = _skillTagMap.GetTag(payload.Skill);

        var card = await _mediator.Send(
            new AddCardCommand(
                workspace.Id,
                SystemLaneNames.ToDo,
                payload.Title,
                description,
                tag,
                CardInsertPosition.Top),
            cancellationToken);

        var cardVm = new CardViewModel
        {
            Id = card.Id,
            Number = card.Number,
            Title = card.Title,
            Description = card.Description,
            LaneName = card.LaneName,
            TagName = card.TagName,
            IsClosed = card.IsClosed,
            GitHubIssueNumber = card.GitHubIssueNumber,
        };

        var saved = await _dialogService.ShowAsync(
            cardVm, workspace.Path, workspace.Id, workspace.GitHubRepo, xamlRoot);

        if (!saved)
            await _mediator.Send(new RemoveCardCommand(card.Id), cancellationToken);
    }

    internal static ConvertToCardPayload? ParsePayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type) ||
                !string.Equals(type.GetString(), "convertToCard", StringComparison.Ordinal))
                return null;

            return new ConvertToCardPayload(
                Skill: ReadString(root, "skill"),
                Title: ReadString(root, "title"),
                Body: ReadString(root, "body"),
                Severity: ReadOptionalString(root, "severity"),
                Location: ReadOptionalString(root, "location"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string? ResolveWorkspacePathFromSource(Uri? sourceUri)
    {
        if (sourceUri is null || !sourceUri.IsFile) return null;
        var dir = new DirectoryInfo(Path.GetDirectoryName(sourceUri.LocalPath) ?? string.Empty);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".bishop")))
                return dir.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            dir = dir.Parent;
        }
        return null;
    }

    private static string BuildDescription(ConvertToCardPayload p)
    {
        var location = string.IsNullOrEmpty(p.Location) ? "(unknown)" : p.Location;
        return $"### Why\n{p.Body}\n\n### Related\nFrom `{p.Skill}` review at `{location}`";
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? string.Empty
            : string.Empty;

    private static string? ReadOptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    internal sealed record ConvertToCardPayload(
        string Skill,
        string Title,
        string Body,
        string? Severity,
        string? Location);
}
