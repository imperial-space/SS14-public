using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Хранит ограниченный журнал аудита Тетради смерти в пределах одного раунда.
/// </summary>
[RegisterComponent, Access(typeof(DeathNoteJournalSystem))]
public sealed partial class DeathNoteJournalRuntimeComponent : Component
{
    /// <summary>
    /// Максимальное число отклонённых попыток записи, сохраняемых для административного журнала.
    /// Подтверждённые записи тетради в этот предел не входят.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxRejectedEntries = 256;

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<uint, LinkedListNode<DeathNoteEntry>> EntryNodes = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public LinkedList<DeathNoteEntry> OrderedEntries = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public Queue<uint> RejectedEntryIds = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public uint NextEntryId = 1;
}
