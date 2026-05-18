using MediatR;

namespace Bishop.App.Skills.LaunchSkill;

public sealed record LaunchSkillCommand(string WorkspacePath, string RenderedCommand) : IRequest<bool>;
