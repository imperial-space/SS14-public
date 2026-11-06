using System.Numerics;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[RegisterComponent]
public sealed partial class HereticPatrinsReachComponent : Component
{
    [ViewVariables]
    public EntityUid CasterUid;
    [ViewVariables]
    public Vector2 TargetWorldPos;
    [ViewVariables]
    public float Speed = 3f;
    [ViewVariables]
    public float MaxRange = 20f;
    [ViewVariables]
    public float TraveledDistance = 0f;
    [ViewVariables]
    public float RustChance = 0.75f;
    [ViewVariables]
    public Vector2i? LastTile;

    [DataField]
    public DamageSpecifier CausticDamage = new() { DamageDict = { ["Caustic"] = 25 } };

    [DataField]
    public float ProjectileArriveRange = 1.5f;

    [DataField]
    public float ProjectileArriveRadius = 0.6f;

    [DataField]
    public float ProjectileArriveAmount = 30f;

    [DataField]
    public float UpdateRadius = 0.6f;

    [DataField]
    public float UpdateAmount = 15f;
}
