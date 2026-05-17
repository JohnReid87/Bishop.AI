using MediatR;

namespace Bishop.App.Ping;

public sealed record PingQuery : IRequest<string>;
