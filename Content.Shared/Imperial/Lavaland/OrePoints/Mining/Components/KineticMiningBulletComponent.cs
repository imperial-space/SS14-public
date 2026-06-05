using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// When attached to a projectile, restricts full damage to targets matching <see cref="TargetWhitelist"/>.
/// All other targets receive <see cref="FallbackDamage"/> instead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class KineticMiningBulletComponent : Component
{
    [DataField]
    public EntityWhitelist? TargetWhitelist;

    [DataField]
    public DamageSpecifier FallbackDamage = new();
}
