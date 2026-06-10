using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.App.Findings.GetPriorFindings;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.App.Context;

public sealed class ArchContextProviderPriorFindingsTests
{
    [Fact]
    public async Task BuildSkillSpecificAsync_ReturnsPriorFindings_ForBishArch()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "ws", Path = "C:\\ws" };
        var mediator = Substitute.For<ISender>();
        var prior = new List<PriorFindingRecord>
        {
            new("hash1", "Bishop.App", "src/F.cs", "Sym", "R", "Title", "dismissed", "Not a real issue.", null),
        };
        mediator.Send(
                Arg.Is<GetPriorFindingsQuery>(q => q.SkillName == "bish-arch" && q.WorkspaceId == workspace.Id),
                Arg.Any<CancellationToken>())
            .Returns(prior);

        var sut = new ArchContextProvider();

        var result = await sut.BuildSkillSpecificAsync(new ContextPackArgs(null), workspace, mediator, default);

        result.Should().NotBeNull();
        var resultType = result!.GetType();
        var priorProp = resultType.GetProperty("PriorFindings")!.GetValue(result);
        priorProp.Should().BeEquivalentTo(prior);
    }

    [Theory]
    [InlineData(typeof(SecurityContextProvider), "bish-security")]
    [InlineData(typeof(TestsContextProvider), "bish-tests")]
    [InlineData(typeof(CoverageContextProvider), "bish-coverage")]
    public async Task SiblingProviders_QueryPriorFindingsForBishPrefixedSkill(Type providerType, string expectedSkill)
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "ws", Path = "C:\\ws" };
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetPriorFindingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PriorFindingRecord>());

        var sut = (IContextProvider)Activator.CreateInstance(providerType)!;

        await sut.BuildSkillSpecificAsync(new ContextPackArgs(null), workspace, mediator, default);

        await mediator.Received(1).Send(
            Arg.Is<GetPriorFindingsQuery>(q => q.SkillName == expectedSkill && q.WorkspaceId == workspace.Id),
            Arg.Any<CancellationToken>());
    }
}
