using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Хранит расписания Тетради смерти текущего раунда и их общую границу отмены.
/// </summary>
[RegisterComponent, Access(typeof(DeathNoteSchedulerSystem))]
public sealed partial class DeathNoteSchedulerRuntimeComponent : Component
{
    public static readonly EntProtoId Prototype = "DeathNoteRoundRuntime";

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<(uint EntryId, DeathNoteScheduledPhase Phase), TimeSpan> ScheduledPhases = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public int RoundId;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool RoundInitialized;
}
