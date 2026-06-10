using Bishop.App.Skills;
using FluentAssertions;

namespace Bishop.Tests.App.Skills;

public sealed class SkillModelOptionsTests
{
    [Fact]
    public void ResolveModelId_WhenSavedIsNull_ReturnsDefault()
    {
        SkillModelOptions.ResolveModelId(null).Should().Be(SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public void ResolveModelId_WhenSavedIsDefault_ReturnsDefault()
    {
        SkillModelOptions.ResolveModelId(SkillModelOptions.DefaultModelId)
            .Should().Be(SkillModelOptions.DefaultModelId);
    }

    [Fact]
    public void ResolveModelId_WhenSavedIsOpus_ReturnsOpus()
    {
        const string opusId = "claude-opus-4-7";

        SkillModelOptions.ResolveModelId(opusId).Should().Be(opusId);
    }

    [Fact]
    public void ResolveModelId_WhenSavedIsFable_ReturnsFable()
    {
        const string fableId = "claude-fable-5";

        SkillModelOptions.ResolveModelId(fableId).Should().Be(fableId);
    }

    [Fact]
    public void ResolveModelId_WhenSavedIsHaiku_ReturnsHaiku()
    {
        const string haikuId = "claude-haiku-4-5-20251001";

        SkillModelOptions.ResolveModelId(haikuId).Should().Be(haikuId);
    }

    [Fact]
    public void DefaultModelId_IsSonnet()
    {
        SkillModelOptions.DefaultModelId.Should().Be("claude-sonnet-4-6");
    }
}
