using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

/// <summary>
/// Описывает ограниченную, явно зарегистрированную причину смерти.
/// </summary>
[Prototype]
public sealed partial class DeathNotePresetPrototype : IPrototype
{
    [IdDataField, ViewVariables(VVAccess.ReadOnly)]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Локализованное название для интерфейса.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public LocId Name { get; private set; } = default!;

    /// <summary>
    /// Буквальные русские и английские варианты для серверного распознавания.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public List<string> Aliases { get; private set; } = new();

    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public DeathNotePresetHandlerType Handler { get; private set; }

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public DeathNotePresetParameters Parameters { get; private set; } = new();

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public DeathNoteDestructionLevel Destruction { get; private set; }

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool Enabled { get; private set; } = true;
}
