using Bishop.Core.Skills;

namespace Bishop.App.Skills;

public static class SkillStaging
{
    public static bool ShouldShowStageDialog(InstalledSkill skill, bool hasCard) =>
        skill.Stage && !hasCard;
}
