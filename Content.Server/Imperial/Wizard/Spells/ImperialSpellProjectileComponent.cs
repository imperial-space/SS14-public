using System.Numerics;
using Content.Server.Imperial.Antimagic;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/datum/action/cooldown/spell/pointed/projectile: снаряд в точку (несколько выстрелов за активацию).</summary>
[RegisterComponent]
public sealed partial class ImperialSpellProjectileComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>projectile_amount: сколько выстрелов за одну активацию.</summary>
    [DataField]
    public int Amount = 1;

    /// <summary>projectiles_per_fire.</summary>
    [DataField]
    public int PerFire = 1;

    /// <summary>Разброс снарядов одного выстрела, градусы.</summary>
    [DataField]
    public float Spread;

    [DataField]
    public float Speed = 20f;

    /// <summary>range: дальность в клетках (null — по прототипу).</summary>
    [DataField]
    public float? Range;

    /// <summary>Дальность растёт с уровнем (у огненного шара 6 + 2 × уровень).</summary>
    [DataField]
    public float RangePerLevel;

    /// <summary>Самонаведение на ближайшее существо у точки клика.</summary>
    [DataField]
    public bool Homing;

    [DataField]
    public float HomingTurnRate = 200f;

    [ViewVariables]
    public int ShotsLeft;

    [DataField]
    public float ProjectileSpellRange = 1.5f;
}
