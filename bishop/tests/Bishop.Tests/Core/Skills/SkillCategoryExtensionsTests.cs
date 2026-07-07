using Bishop.Core.Skills;
using FluentAssertions;

namespace Bishop.Tests.Core.Skills;

public sealed class SkillCategoryExtensionsTests
{
    [Theory]
    [InlineData(SkillCategory.Code)]
    [InlineData(SkillCategory.Tests)]
    [InlineData(SkillCategory.Review)]
    public void IsMonitored_ReviewCategories_ReturnsTrue(SkillCategory category)
        => category.IsMonitored().Should().BeTrue();

    [Theory]
    [InlineData(SkillCategory.Discuss)]
    [InlineData(SkillCategory.Execute)]
    [InlineData(SkillCategory.Setup)]
    [InlineData(SkillCategory.Meta)]
    [InlineData(SkillCategory.Other)]
    public void IsMonitored_NonReviewCategories_ReturnsFalse(SkillCategory category)
        => category.IsMonitored().Should().BeFalse();
}
