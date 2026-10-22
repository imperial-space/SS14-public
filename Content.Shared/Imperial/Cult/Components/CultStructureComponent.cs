using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Постройка культа (obj/structure/destructible/cult). Раздатчики выдают предметы раз в 5 минут.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultStructureComponent : Component
{
    [DataField]
    public CultStructureType StructureType = CultStructureType.None;

    /// <summary>use_cooldown_duration.</summary>
    [DataField]
    public TimeSpan UseCooldown = TimeSpan.FromMinutes(5);

    /// <summary>cult_examine_tip.</summary>
    [DataField]
    public LocId? ExamineTip;

    /// <summary>succcess_message.</summary>
    [DataField]
    public LocId? SuccessMessage;

    /// <summary>break_message.</summary>
    [DataField]
    public LocId? BreakMessage;

    /// <summary>Базовое состояние спрайта (без "_off").</summary>
    [DataField]
    public string BaseState = "pylon";

    [DataField]
    public List<CultDispenserOption> Options = new();

    [ViewVariables] public TimeSpan NextUse;
    [ViewVariables] public bool Concealed;
}

[DataDefinition]
public sealed partial class CultDispenserOption
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
