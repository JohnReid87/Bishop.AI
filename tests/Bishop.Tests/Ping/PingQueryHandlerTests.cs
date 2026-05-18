using Bishop.App.Ping;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Bishop.Tests.Ping;

public class PingQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPong()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(PingQueryHandler).Assembly));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new PingQuery());

        result.Should().Be("pong");
    }
}
