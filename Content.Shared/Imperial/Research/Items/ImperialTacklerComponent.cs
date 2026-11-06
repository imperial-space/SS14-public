using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Перчатки для захвата TG (gloves/tackler): действие «Бросок» — прыжок к цели, сбивающий её с ног.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialTacklerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ImperialActionTackle";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>Дальность прыжка (tackle_range).</summary>
    [DataField]
    public float Range = 4f;

    /// <summary>Скорость прыжка (tackle_speed).</summary>
    [DataField]
    public float Speed = 8f;

    /// <summary>Длительность сбивания цели (base_knockdown), секунды.</summary>
    [DataField]
    public TimeSpan Knockdown = TimeSpan.FromSeconds(1f);

    /// <summary>Бонус к броску (skill_mod): больше — чаще цель падает, а сам прыгун остаётся на ногах.</summary>
    [DataField]
    public int SkillMod = 1;

    /// <summary>Урон стамине прыгуна (stamina_cost).</summary>
    [DataField]
    public float StaminaCost = 25f;

    /// <summary>Шанс врезаться при промахе (rocket gloves).</summary>
    [DataField]
    public float CrashChance;
}
