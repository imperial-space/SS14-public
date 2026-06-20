using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// Bullet that deals bonus damage in low-pressure environments (space / vacuum).
/// The base damage is defined in the projectile, the bonus is applied here.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AtmoBulletComponent : Component
{
    /// <summary>
    /// Bonus damage applied when the tile has low pressure (below <see cref="PressureThreshold"/> moles).
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier BonusDamage = new();

    /// <summary>
    /// Total moles of gas below which bonus damage activates.
    /// A standard fully-pressurized tile has ~2500 moles; space has 0.
    /// </summary>
    [DataField]
    public float PressureThreshold = 30f;
}
