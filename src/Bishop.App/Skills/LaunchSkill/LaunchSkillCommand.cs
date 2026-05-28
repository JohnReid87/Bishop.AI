using Bishop.App.Services.Terminal;
using MediatR;

namespace Bishop.App.Skills.LaunchSkill;

public sealed record LaunchSkillCommand(string WorkspacePath, string RenderedCommand, TerminalSnap? Snap = null, string? ModelId = null) : IRequest<bool>;
