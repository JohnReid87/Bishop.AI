using Bishop.Core.Skills;
using MediatR;

namespace Bishop.App.Skills.DiscoverSkills;

public sealed class DiscoverSkillsQueryHandler : IRequestHandler<DiscoverSkillsQuery, IReadOnlyList<InstalledSkill>>
{
    private readonly string _skillsRoot;

    public DiscoverSkillsQueryHandler()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "skills"))
    { }

    internal DiscoverSkillsQueryHandler(string skillsRoot) => _skillsRoot = skillsRoot;

    public Task<IReadOnlyList<InstalledSkill>> Handle(DiscoverSkillsQuery request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_skillsRoot))
            return Task.FromResult<IReadOnlyList<InstalledSkill>>([]);

        var skills = new List<InstalledSkill>();

        foreach (var dir in Directory.EnumerateDirectories(_skillsRoot))
        {
            var skill = TryReadSkill(dir);
            if (skill is not null)
                skills.Add(skill);
        }

        return Task.FromResult<IReadOnlyList<InstalledSkill>>(skills);
    }

    private static InstalledSkill? TryReadSkill(string dir)
    {
        var skillFile = Path.Combine(dir, "SKILL.md");
        if (!File.Exists(skillFile))
            return null;

        var doc = SkillMdParser.Parse(File.ReadAllText(skillFile));
        return InstalledSkillFactory.TryCreate(doc, skillFile);
    }
}
