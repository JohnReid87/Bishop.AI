using Bishop.Life.Core;
using Bishop.Life.Core.Schema;
using FluentAssertions;

namespace Bishop.Life.Tests;

public class LifeMutationCoordinatorTests
{
    [Fact]
    public void ApplyMutation_Idle_SavesPlan()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());
        var coordinator = new LifeMutationCoordinator(service);

        var plan = new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } };
        var result = coordinator.ApplyMutation(plan);

        result.Should().Be(MutationResult.Saved);
        service.Exists().Should().BeTrue();
    }

    [Fact]
    public void ApplyMutation_WhileStandupInFlight_IsRejected()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());
        var coordinator = new LifeMutationCoordinator(service);

        coordinator.NoteStandupLaunched();
        var result = coordinator.ApplyMutation(new LifePlan());

        result.Should().Be(MutationResult.RejectedStandupInFlight);
        service.Exists().Should().BeFalse();
    }

    [Fact]
    public void NoteExternalReload_ClearsInFlightFlag()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));

        coordinator.NoteStandupLaunched();
        coordinator.NoteExternalReload();

        coordinator.StandupInFlight.Should().BeFalse();
        coordinator.ApplyMutation(new LifePlan()).Should().Be(MutationResult.Saved);
    }

    [Fact]
    public void StateChanged_FiresOnLaunchAndOnExternalReload()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));
        var events = 0;
        coordinator.StateChanged += (_, _) => events++;

        coordinator.NoteStandupLaunched();
        coordinator.NoteStandupLaunched(); // idempotent
        coordinator.NoteExternalReload();
        coordinator.NoteExternalReload(); // idempotent

        events.Should().Be(2);
    }
}
