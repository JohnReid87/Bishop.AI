using Bishop.Core.Skills;

namespace Bishop.App.Skills;

public sealed record SkillMenuItem(string Name, InstalledSkill Skill, bool HasSeparatorAfter);
