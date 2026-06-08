using Bishop.Life.Core.Schema;

namespace Bishop.Life.Core;

public enum MutationResult
{
    Saved,
    RejectedStandupInFlight,
}

/// <summary>
/// Gates inline mutations (star, check, title-edit) on whether a stand-up is
/// currently rewriting the plan file. Wraps <see cref="LifePlanFileService"/>
/// so the App layer has a single seam to call.
/// </summary>
/// <remarks>
/// The in-flight signal is best-effort: it flips on when the host launches a
/// stand-up and flips off on the next externally-observed file change. The
/// host is responsible for distinguishing its own writes from external ones
/// before calling <see cref="NoteExternalReload"/>.
/// </remarks>
public sealed class LifeMutationCoordinator
{
    private readonly LifePlanFileService _service;
    private bool _standupInFlight;

    public LifeMutationCoordinator(LifePlanFileService service)
    {
        _service = service;
    }

    public bool StandupInFlight => _standupInFlight;

    public event EventHandler? StateChanged;

    public void NoteStandupLaunched()
    {
        if (_standupInFlight) return;
        _standupInFlight = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NoteExternalReload()
    {
        if (!_standupInFlight) return;
        _standupInFlight = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public MutationResult ApplyMutation(LifePlan plan)
    {
        if (_standupInFlight) return MutationResult.RejectedStandupInFlight;
        _service.Save(plan);
        return MutationResult.Saved;
    }
}
