using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.AshDrake;

/// <summary>
/// Attached to every fire tile spawned by the AshDrake.
/// Deals continuous damage to players standing on the tile until it despawns.
/// </summary>
[RegisterComponent]
public sealed partial class AshDrakeFireTileComponent : Component
{
    /// <summary>The drake that owns this fire (used as damage origin).</summary>
    public EntityUid DrakeUid;

    /// <summary>Heat damage per tick.</summary>
    public float Damage;

    /// <summary>Next time damage should be applied to players on this tile.</summary>
    public TimeSpan NextDamageTime;
}
