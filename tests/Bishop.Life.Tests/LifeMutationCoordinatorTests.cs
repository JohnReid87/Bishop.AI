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
        coordinator.ApplyMutation(plan);

        service.Exists().Should().BeTrue();
    }

    [Fact]
    public void ApplyMutation_WhileStandupInFlight_StillSaves()
    {
        using var tmp = new TempDir();
        var service = new LifePlanFileService(tmp.FilePath());
        var coordinator = new LifeMutationCoordinator(service);

        coordinator.NoteStandupLaunched();
        coordinator.ApplyMutation(new LifePlan { Meta = new Meta { CreatedAt = DateTimeOffset.UtcNow } });

        service.Exists().Should().BeTrue();
    }

    [Fact]
    public void NoteStandupAborted_ClearsInFlightFlag()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));

        coordinator.NoteStandupLaunched();
        coordinator.NoteStandupAborted();

        coordinator.StandupInFlight.Should().BeFalse();
    }

    [Fact]
    public void NoteStandupAborted_IsIdempotentWhenNotInFlight()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));
        var events = 0;
        coordinator.StateChanged += (_, _) => events++;

        coordinator.NoteStandupAborted();

        coordinator.StandupInFlight.Should().BeFalse();
        events.Should().Be(0);
    }

    [Fact]
    public void StateChanged_FiresOnLaunchAndAbort()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));
        var events = 0;
        coordinator.StateChanged += (_, _) => events++;

        coordinator.NoteStandupLaunched();
        coordinator.NoteStandupLaunched(); // idempotent
        coordinator.NoteStandupAborted();
        coordinator.NoteStandupAborted(); // idempotent

        events.Should().Be(2);
    }

    [Fact]
    public void NoteAddLaunched_SetsInFlightAndFiresStateChanged()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));
        var events = 0;
        coordinator.StateChanged += (_, _) => events++;

        coordinator.NoteAddLaunched();
        coordinator.NoteAddLaunched(); // idempotent

        coordinator.AddInFlight.Should().BeTrue();
        events.Should().Be(1);
    }

    [Fact]
    public void NoteAddAborted_ClearsInFlightFlag()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));

        coordinator.NoteAddLaunched();
        coordinator.NoteAddAborted();

        coordinator.AddInFlight.Should().BeFalse();
    }

    [Fact]
    public void NoteAddAborted_IsIdempotentWhenNotInFlight()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));
        var events = 0;
        coordinator.StateChanged += (_, _) => events++;

        coordinator.NoteAddAborted();

        coordinator.AddInFlight.Should().BeFalse();
        events.Should().Be(0);
    }

    [Fact]
    public void AddAndStandup_InFlightFlagsAreIndependent()
    {
        using var tmp = new TempDir();
        var coordinator = new LifeMutationCoordinator(new LifePlanFileService(tmp.FilePath()));

        coordinator.NoteStandupLaunched();
        coordinator.NoteAddLaunched();

        coordinator.StandupInFlight.Should().BeTrue();
        coordinator.AddInFlight.Should().BeTrue();

        coordinator.NoteStandupAborted();

        coordinator.StandupInFlight.Should().BeFalse();
        coordinator.AddInFlight.Should().BeTrue();
    }
}
