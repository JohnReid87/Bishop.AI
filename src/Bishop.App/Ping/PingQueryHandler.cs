using MediatR;

namespace Bishop.App.Ping;

internal sealed class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken cancellationToken)
        => Task.FromResult("pong");
}
