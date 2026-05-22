using Bishop.App.Workspaces.CreateWorkspace;
using Bishop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Bishop.Tests.App.Workspaces;

public sealed class ParallelDispatchIsolationTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _fixture;

    public ParallelDispatchIsolationTests(DbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ParallelDispatches_EachReceiveTheirOwnDbContext()
    {
        // Acceptance test for card #134: handler dispatches must obtain a fresh
        // context per dispatch rather than sharing the root-scoped one. We hand
        // the handlers a tracking factory that records every context it produces,
        // then verify the two parallel dispatches received distinct instances —
        // which means each dispatch had its own ChangeTracker.

        // Arrange
        var tracker = new TrackingDbContextFactory(_fixture.Factory);
        var handler1 = new CreateWorkspaceCommandHandler(tracker);
        var handler2 = new CreateWorkspaceCommandHandler(tracker);
        var name1 = $"par-a-{Guid.NewGuid():N}"[..18];
        var name2 = $"par-b-{Guid.NewGuid():N}"[..18];

        // Act — two dispatches in parallel
        var results = await Task.WhenAll(
            handler1.Handle(new CreateWorkspaceCommand(name1, $@"C:\{name1}"), default),
            handler2.Handle(new CreateWorkspaceCommand(name2, $@"C:\{name2}"), default));

        // Assert — two contexts were created, and they are distinct instances.
        // Distinct contexts guarantee distinct change trackers (EF Core
        // exposes the tracker via DbContext.ChangeTracker, scoped per instance).
        tracker.Contexts.Should().HaveCount(2);
        tracker.Contexts[0].Should().NotBeSameAs(tracker.Contexts[1]);

        // And both workspaces were actually persisted.
        results.Should().HaveCount(2);
        results.Select(w => w.Name).Should().Contain([name1, name2]);
    }

    private sealed class TrackingDbContextFactory : IDbContextFactory<BishopDbContext>
    {
        private readonly IDbContextFactory<BishopDbContext> _inner;
        private readonly List<BishopDbContext> _contexts = [];
        private readonly object _lock = new();

        public TrackingDbContextFactory(IDbContextFactory<BishopDbContext> inner) => _inner = inner;

        public IReadOnlyList<BishopDbContext> Contexts
        {
            get { lock (_lock) return _contexts.ToList(); }
        }

        public BishopDbContext CreateDbContext()
        {
            var ctx = _inner.CreateDbContext();
            lock (_lock) _contexts.Add(ctx);
            return ctx;
        }
    }
}
