using Bishop.Life.Core.Schema;

namespace Bishop.Life.Core;

/// <summary>
/// Tracks whether a stand-up or add subprocess is currently rewriting the
/// plan file and wraps <see cref="LifePlanFileService"/> so the App layer
/// has a single seam for inline mutations (star, check, title-edit).
/// </summary>
/// <remarks>
/// Each in-flight flag flips on when the host launches the corresponding
/// skill and flips off when the host reports the user has returned to the
/// window. Mutations are not gated on the flags — last-write-wins is
/// acceptable for this single-user local app.
/// </remarks>
public sealed class LifeMutationCoordinator
{
    private readonly LifePlanFileService _service;
    private bool _standupInFlight;
    private bool _addInFlight;

    public LifeMutationCoordinator(LifePlanFileService service)
    {
        _service = service;
    }

    public bool StandupInFlight => _standupInFlight;

    public bool AddInFlight => _addInFlight;

    public event EventHandler? StateChanged;

    public void NoteStandupLaunched()
    {
        if (_standupInFlight) return;
        _standupInFlight = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the stand-up in-flight flag. Called by the host when the user
    /// reactivates the window after a stand-up that completed or was aborted.
    /// </summary>
    public void NoteStandupAborted()
    {
        if (!_standupInFlight) return;
        _standupInFlight = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NoteAddLaunched()
    {
        if (_addInFlight) return;
        _addInFlight = true;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the add in-flight flag. Called by the host when the user
    /// reactivates the window after an add that completed or was aborted.
    /// </summary>
    public void NoteAddAborted()
    {
        if (!_addInFlight) return;
        _addInFlight = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyMutation(LifePlan plan) => _service.Save(plan);
}
