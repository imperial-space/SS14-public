using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// gun/energy/recharge/kinetic_accelerator: самозаряжающийся акселератор с модулями (max_mod_capacity).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticAcceleratorComponent : Component
{
    /// <summary>max_mod_capacity.</summary>
    [DataField]
    public int MaxModCapacity = 100;

    [DataField]
    public string ContainerId = "mining_upgrades";

    /// <summary>recharge_time без модулей, секунды.</summary>
    [DataField]
    public float RechargeTime = 1.6f;

    /// <summary>projectile/kinetic range, клетки.</summary>
    [DataField]
    public int Range = 3;

    [DataField]
    public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");
}

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

/// <summary>
/// projectile/kinetic: урон ×pressure_decrease, если давление на клетке выше LAVALAND_EQUIPMENT_EFFECT_PRESSURE.
/// </summary>
[RegisterComponent]
public sealed partial class KineticProjectileComponent : Component
{
    [DataField]
    public float PressureDecrease = 0.25f;

    /// <summary>LAVALAND_EQUIPMENT_EFFECT_PRESSURE, кПа.</summary>
    [DataField]
    public float PressureThreshold = 50f;

    /// <summary>
    /// Давление планетной атмосферы Лаваленда выше 50 кПа, поэтому на карте Лаваленда
    /// «низким» считается всё, что не выше этого значения (наружный воздух планеты).
    /// </summary>
    [DataField]
    public float LavalandPressure = 85f;

    [ViewVariables]
    public bool PressureDecreaseActive;

    [ViewVariables]
    public bool Struck;

    [ViewVariables]
    public EntityUid? Gun;

    [ViewVariables]
    public EntityUid? Firer;
}
