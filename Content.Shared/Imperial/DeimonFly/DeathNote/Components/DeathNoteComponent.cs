using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Настраиваемая часть обычной Тетради смерти.
/// Изменяемое серверное состояние хранится отдельно и не синхронизируется клиенту.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public DeathNoteOwnershipChannel OwnershipChannel = DeathNoteOwnershipChannel.First;

    /// <summary>
    /// Whether the first character to take this notebook into a hand becomes
    /// its persistent owner. Shinigami notebooks deliberately disable this.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool AssignsPermanentOwner = true;

    /// <summary>
    /// Whether a temporary holder sees all three independent Shinigami
    /// visibility channels instead of only <see cref="OwnershipChannel"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool GrantsAllShinigamiVisibility;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxNameLength = 64;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxCauseLength = 500;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxTimeLength = 16;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxEntryTextLength = 640;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxEntries = 360;

    /// <summary>
    /// Количество физических белых страниц после чёрного форзаца с правилами.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int WritablePageCount = 30;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int EntriesPerPage = 12;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan DefaultDelay = TimeSpan.FromSeconds(40);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan SubmissionCooldown = TimeSpan.FromSeconds(2);

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan InvalidAttemptLogCooldown = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Пресет, применяемый при пустой причине.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<DeathNotePresetPrototype> DefaultPreset = "DeathNoteHeartAttack";

    /// <summary>
    /// Тег подходящего письменного инструмента.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public ProtoId<TagPrototype> WritingTag = "Write";

    /// <summary>
    /// Единый локализованный текст правил на чёрном форзаце.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public LocId RulePage = "death-note-rule-page";

    /// <summary>
    /// Если включено, с началом исполнения судьбы цель становится недоступной для дефибрилляции и клонирования.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool MakeTargetsUnrevivable;
}
