using System.Threading;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Хранит расписания Тетради смерти текущего раунда и их общую границу отмены.
/// </summary>
[RegisterComponent, Access(typeof(DeathNoteSchedulerSystem))]
public sealed partial class DeathNoteSchedulerRuntimeComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public HashSet<(uint EntryId, DeathNoteScheduledPhase Phase)> ScheduledPhases = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public CancellationTokenSource RoundCancellation = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public int RoundId;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool RoundInitialized;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool CancellationDisposed;
}
