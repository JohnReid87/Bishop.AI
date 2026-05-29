using Bishop.App.Context.ContextPack;
using Bishop.App.Context.ContextPack.Providers;
using Bishop.Core;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Bishop.Tests.App.Context;

public sealed class MutationContextProviderTests
{
    private readonly MutationContextProvider _sut = new();

    [Fact]
    public void SkillName_IsMutation()
    {
        _sut.SkillName.Should().Be("mutation");
    }

    [Fact]
    public void RequiredSections_ContainsExpectedSections()
    {
        _sut.RequiredSections.Should().BeEquivalentTo(new[]
        {
            "Shell selection",
            "Card model",
            "Findings Recording Procedure"
        });
    }

    [Fact]
    public async Task BuildSkillSpecificAsync_ReturnsNull_WhenCardArgProvided()
    {
        // Arrange
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "test", Path = "C:\\test" };
        var mediator = Substitute.For<ISender>();

        // Act
        var result = await _sut.BuildSkillSpecificAsync(new ContextPackArgs(42), workspace, mediator, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildSkillSpecificAsync_ReturnsNull_WhenCardArgIsNull()
    {
        // Arrange
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "test", Path = "C:\\test" };
        var mediator = Substitute.For<ISender>();

        // Act
        var result = await _sut.BuildSkillSpecificAsync(new ContextPackArgs(null), workspace, mediator, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildSkillSpecificAsync_ReturnsNull_WhenCancellationTokenCancelled()
    {
        // Arrange
        var workspace = new Workspace { Id = Guid.NewGuid(), Name = "test", Path = "C:\\test" };
        var mediator = Substitute.For<ISender>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.BuildSkillSpecificAsync(new ContextPackArgs(null), workspace, mediator, cts.Token);

        // Assert
        result.Should().BeNull();
    }
}
