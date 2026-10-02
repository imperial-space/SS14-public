using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Касты ксеноморфов (code/modules/mob/living/carbon/alien).</summary>
[Serializable, NetSerializable]
public enum XenoCaste : byte
{
    Larva,
    Hunter,
    Sentinel,
    Drone,
    Praetorian,
    Queen,
}

/// <summary>Ксеноморф: каста, узел улья (hivenode), связь с ульем.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenomorphComponent : Component
{
    [DataField, AutoNetworkedField]
    public XenoCaste Caste;

    /// <summary>Прототип, в который эволюционирует личинка/трутень/преторианец (для однозначных эволюций).</summary>
    [DataField]
    public EntProtoId? EvolvesTo;

    /// <summary>Цена эволюции в плазме.</summary>
    [DataField]
    public float EvolveCost;

    /// <summary>hivenode/recent_queen_death: до этого момента улей ослаблен и эволюция недоступна.</summary>
    [ViewVariables]
    public TimeSpan NoQueenUntil;

    /// <summary>alien_queen_finder: насколько далеко королева (-1 — не чувствуется, 0 рядом, 1/2/3 — near/med/far).</summary>
    [ViewVariables, AutoNetworkedField]
    public sbyte QueenDistance = -1;

    /// <summary>alien_queen_finder: направление на королеву.</summary>
    [ViewVariables, AutoNetworkedField]
    public Robust.Shared.Maths.Direction QueenDirection;
}

/// <summary>
/// Предмет, который ксеноморф может взять в руки (alien/xeno_allowed_items для itempicky):
/// лицехват, королевский паразит, игрушечный ксено.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoHoldableComponent : Component;

/// <summary>Плазменный сосуд (organ/alien/plasmavessel).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoPlasmaComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Plasma = 100;

    [DataField, AutoNetworkedField]
    public float MaxPlasma = 250;

    /// <summary>plasma_rate: прирост в секунду на сорняках (при ранах — половина), вне сорняков — 0.1 от него.</summary>
    [DataField]
    public float PlasmaRate = 5;

    /// <summary>heal_rate: лечение в секунду на сорняках, отдельно brute, burn и oxy.</summary>
    [DataField]
    public float HealRate = 2.5f;

    /// <summary>plasma_display: иконка power_display, число рисует клиент.</summary>
    [DataField]
    public string Alert = "XenoPlasma";
}

/// <summary>Эмбрион ксеноморфа в теле носителя (body_egg/alien_embryo). Стадия видна ксеноморфам.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoEmbryoComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Stage;

    /// <summary>growth_time: время на одну стадию.</summary>
    [DataField]
    public TimeSpan GrowthTime = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextStage;

    [ViewVariables]
    public bool Bursting;

    [ViewVariables]
    public EntityUid? BurstSpawner;

    [ViewVariables]
    public TimeSpan BurstDeadline;

    /// <summary>attempt_grow(gib_on_success = FALSE): личинка выползает, не разрывая носителя.</summary>
    [ViewVariables]
    public bool NoGibBurst;
}

[Serializable, NetSerializable]
public enum XenoVisuals : byte
{
    /// <summary>Личинка: стадия роста 0..2.</summary>
    LarvaStage,

    /// <summary>Личинка прячется под столами.</summary>
    Hidden,

    /// <summary>Яйцо: XenoEggState.</summary>
    EggState,

    /// <summary>Лицехват: XenoFacehuggerState.</summary>
    FacehuggerState,
}

[Serializable, NetSerializable]
public enum XenoEggState : byte
{
    Growing,
    Grown,
    Opening,
    Hatched,
}

[Serializable, NetSerializable]
public enum XenoFacehuggerState : byte
{
    Active,
    Idle,
    Dead,
    Impregnated,
}

#region Действия

/// <summary>Действие ксеноморфа с ценой в плазме (cooldown/alien с plasma_cost).</summary>
public abstract partial class XenoInstantActionEvent : InstantActionEvent
{
    [DataField]
    public float PlasmaCost;
}

public abstract partial class XenoEntityTargetActionEvent : EntityTargetActionEvent
{
    [DataField]
    public float PlasmaCost;
}

public abstract partial class XenoWorldTargetActionEvent : WorldTargetActionEvent
{
    [DataField]
    public float PlasmaCost;
}

/// <summary>Plant Weeds (50).</summary>
public sealed partial class XenoPlantWeedsActionEvent : XenoInstantActionEvent;

/// <summary>Secrete Resin (55): стена, мембрана или гнездо.</summary>
public sealed partial class XenoSecreteResinActionEvent : XenoInstantActionEvent;

/// <summary>Whisper (10).</summary>
public sealed partial class XenoWhisperActionEvent : XenoEntityTargetActionEvent;

/// <summary>Transfer Plasma.</summary>
public sealed partial class XenoTransferPlasmaActionEvent : XenoEntityTargetActionEvent;

/// <summary>Corrosive Acid (200).</summary>
public sealed partial class XenoAcidActionEvent : XenoEntityTargetActionEvent;

/// <summary>Spit Neurotoxin (50).</summary>
public sealed partial class XenoNeurotoxinActionEvent : XenoWorldTargetActionEvent;

/// <summary>Sneak (страж).</summary>
public sealed partial class XenoSneakActionEvent : XenoInstantActionEvent;

/// <summary>Pounce (охотник).</summary>
public sealed partial class XenoLeapActionEvent : XenoWorldTargetActionEvent;

/// <summary>Tail Sweep (преторианец).</summary>
public sealed partial class XenoTailSweepActionEvent : XenoInstantActionEvent;

/// <summary>Devour: пожрать того, кого тащишь.</summary>
public sealed partial class XenoDevourActionEvent : XenoEntityTargetActionEvent;

/// <summary>Regurgitate.</summary>
public sealed partial class XenoRegurgitateActionEvent : XenoInstantActionEvent;

/// <summary>Lay Egg (75, королева).</summary>
public sealed partial class XenoLayEggActionEvent : XenoInstantActionEvent;

/// <summary>Create Royal Parasite (500, королева).</summary>
public sealed partial class XenoRoyalParasiteActionEvent : XenoInstantActionEvent;

/// <summary>Evolve: у личинки — выбор касты, у трутня и преторианца — следующая ступень.</summary>
public sealed partial class XenoEvolveActionEvent : XenoInstantActionEvent;

/// <summary>Hide (личинка).</summary>
public sealed partial class XenoHideActionEvent : XenoInstantActionEvent;

#endregion

#region Радиальное меню

[Serializable, NetSerializable]
public enum XenoRadialUiKey : byte
{
    Resin,
    Evolve,
}

[Serializable, NetSerializable]
public sealed class XenoRadialOption(string id, string name, SpriteSpecifier icon)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly SpriteSpecifier Icon = icon;
}

[Serializable, NetSerializable]
public sealed class XenoRadialState(List<XenoRadialOption> options) : BoundUserInterfaceState
{
    public readonly List<XenoRadialOption> Options = options;
}

[Serializable, NetSerializable]
public sealed class XenoRadialPickMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
}

#endregion

[Serializable, NetSerializable]
public sealed partial class XenoDevourDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class XenoNestEscapeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Извлечение эмбриона режущим инструментом (замена хирургии SS13).</summary>
[Serializable, NetSerializable]
public sealed partial class XenoEmbryoRemovalDoAfterEvent : SimpleDoAfterEvent;
