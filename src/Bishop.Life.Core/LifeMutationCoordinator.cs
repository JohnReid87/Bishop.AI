using Bishop.Life.Core.Schema;

namespace Bishop.Life.Core;

/// <summary>
/// Tracks whether a stand-up subprocess is currently rewriting the plan file
/// and wraps <see cref="LifePlanFileService"/> so the App layer has a single
/// seam for inline mutations (star, check, title-edit).
/// </summary>
/// <remarks>
/// The in-flight flag flips on when the host launches a stand-up and flips
/// off when the host reports the user has returned to the window. Mutations
/// are not gated on the flag — last-write-wins is acceptable for this
/// single-user local app.
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

    /// <summary>
    /// Clears the in-flight flag. Called by the host when the user reactivates
    /// the window after a stand-up that completed or was aborted.
    /// </summary>
    public void NoteStandupAborted()
    {
        if (!_standupInFlight) return;
        _standupInFlight = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMutation(LifePlan plan) => _service.Save(plan);
}
