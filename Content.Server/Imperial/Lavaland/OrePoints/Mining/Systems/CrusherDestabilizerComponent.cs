using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Imperial.Lavaland.MegafaunaTracker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>projectile/destabilizer.</summary>
[RegisterComponent]
public sealed partial class CrusherDestabilizerComponent : Component
{
    [ViewVariables]
    public EntityUid? Crusher;

    [ViewVariables]
    public EntityUid? Firer;

    [ViewVariables]
    public bool Slow;

    [ViewVariables]
    public bool Slowed;

    /// <summary>raptor_feather: ignore_allies.</summary>
    [ViewVariables]
    public bool IgnoreAllies;

    [DataField]
    public float DestabilizerHitMineralRange = 1.5f;
}
