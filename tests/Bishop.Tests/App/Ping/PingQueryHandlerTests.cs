using Bishop.App.Ping;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Bishop.Tests.App.Ping;

public class PingQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPong()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingQueryHandler).Assembly));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new PingQuery());

        // Assert
        result.Should().Be("pong");
    }
}
