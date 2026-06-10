using Bishop.App.Tags.ListTags;
using Bishop.App.Workspaces.GetWorkspace;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Bishop.Tests.App;

public class MediatRPipelineSmokeTests
{
    [Fact]
    public async Task Pipeline_ResolvesAndDispatches_RealHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(GetWorkspaceQueryHandler).Assembly));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new ListTagsQuery());

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }
}
