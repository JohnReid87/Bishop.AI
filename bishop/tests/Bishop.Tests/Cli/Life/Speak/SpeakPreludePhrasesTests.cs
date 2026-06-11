using Bishop.Cli.Life.Speak;
using FluentAssertions;

namespace Bishop.Tests.Cli.Life.Speak;

public sealed class SpeakPreludePhrasesTests
{
    [Fact]
    public void All_HasBetweenFourAndSixPhrases()
    {
        SpeakPreludePhrases.All.Count.Should().BeInRange(4, 6);
    }

    [Fact]
    public void All_ContainsOnlyNonEmptyTrimmedPhrases()
    {
        SpeakPreludePhrases.All.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p));
    }

    [Fact]
    public void Pick_ReturnsAPhraseFromThePool()
    {
        var random = new Random(42);
        var phrase = SpeakPreludePhrases.Pick(random);
        SpeakPreludePhrases.All.Should().Contain(phrase);
    }

    [Fact]
    public void Pick_OverManyCallsCoversEveryPhrase()
    {
        var random = new Random(1);
        var seen = new HashSet<string>();
        for (int i = 0; i < 500; i++)
            seen.Add(SpeakPreludePhrases.Pick(random));
        seen.Should().BeEquivalentTo(SpeakPreludePhrases.All);
    }

    [Fact]
    public void Pick_NullRandom_Throws()
    {
        var act = () => SpeakPreludePhrases.Pick(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
