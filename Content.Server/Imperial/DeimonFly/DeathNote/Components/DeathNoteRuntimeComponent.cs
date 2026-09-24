namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Изменяемое серверное состояние конкретной тетради.
/// Компонент добавляется системой и никогда не синхронизируется клиенту.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteRuntimeComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? OwnerEntity;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? CurrentHolder;

    [ViewVariables(VVAccess.ReadOnly)]
    public readonly List<uint> EntryIds = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextSubmissionTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextInvalidAttemptLogTime;

    [ViewVariables(VVAccess.ReadOnly)]
    public uint SubmissionRevision = 1;
}
