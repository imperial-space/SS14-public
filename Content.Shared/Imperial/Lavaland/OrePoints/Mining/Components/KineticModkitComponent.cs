using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// borg/upgrade/modkit: модуль кинетического акселератора.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticModkitComponent : Component
{
    /// <summary>cost — процент вместимости.</summary>
    [DataField]
    public int Cost = 30;

    /// <summary>denied_type: модули одной группы ограничены <see cref="MaxOfGroup"/>.</summary>
    [DataField]
    public string? Group;

    /// <summary>maximum_of_type; 0 — не больше одного модуля группы.</summary>
    [DataField]
    public int MaxOfGroup;

    /// <summary>range: + клетки полёта.</summary>
    [DataField]
    public int Range;

    /// <summary>damage: + урон снаряда.</summary>
    [DataField]
    public float Damage;

    /// <summary>cooldown modifier в секундах: вычитается из recharge_time (отрицательный — замедляет).</summary>
    [DataField]
    public float Cooldown;

    /// <summary>cooldown/aoe: взрыв на месте попадания.</summary>
    [DataField]
    public bool Aoe;

    /// <summary>turf_aoe: взрыв дробит породу вокруг.</summary>
    [DataField]
    public bool AoeTurfs;

    /// <summary>damage_modifier: множитель урона по шахтёрским мобам вокруг.</summary>
    [DataField]
    public float AoeMobDamage;

    /// <summary>cooldown/repeater: попадание по живому или породе — перезарядка ×0.25.</summary>
    [DataField]
    public bool Repeater;

    /// <summary>lifesteal: лечение стрелка при попадании по живой цели.</summary>
    [DataField]
    public float Lifesteal;

    /// <summary>indoors: множитель pressure_decrease.</summary>
    [DataField]
    public float PressureMultiplier = 1f;

    /// <summary>human_passthrough: снаряды проходят сквозь гуманоидов.</summary>
    [DataField]
    public bool HumanPassthrough;

    /// <summary>resonator_blasts: оставляет и подрывает резонансные поля.</summary>
    [DataField]
    public bool ResonatorBlasts;

    [DataField]
    public float ResonatorMultiplier = 0.25f;

    /// <summary>bounty (death syphon): прирост урона по виду за убийство.</summary>
    [DataField]
    public bool Bounty;

    [DataField]
    public float BountyModifier = 1.25f;

    [DataField]
    public float BountyMax = 25f;

    /// <summary>bounties_reaped: прототип моба → бонус урона.</summary>
    [ViewVariables]
    public Dictionary<string, float> Bounties = new();
}
