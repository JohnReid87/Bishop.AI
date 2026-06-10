using Bishop.Core.Skills;
using MediatR;

namespace Bishop.App.Skills.DiscoverSkills;

public sealed record DiscoverSkillsQuery : IRequest<IReadOnlyList<InstalledSkill>>;
