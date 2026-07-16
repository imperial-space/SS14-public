using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.LavaStaff;

/// <summary>
/// A staff that can toggle lava/basalt tiles and fires lava projectiles.
/// Equipped in the back slot like a backpack.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavaStaffComponent : Component
{
    /// <summary>Cooldown between tile changes.</summary>
    [DataField]
    public TimeSpan TileCooldown = TimeSpan.FromSeconds(1);

    /// <summary>Last time the tile was changed.</summary>
    [ViewVariables]
    public TimeSpan LastTileChange = TimeSpan.Zero;

    /// <summary>Prototype of the lava floor entity to spawn.</summary>
    [DataField]
    public string LavaEntityPrototype = "FloorLavaEntity";

    /// <summary>Tile ID to revert to when removing lava.</summary>
    [DataField]
    public string BasaltTileId = "FloorBasalt";

    /// <summary>Projectile prototype to fire on tile interaction.</summary>
    [DataField]
    public string ProjectilePrototype = "BulletLavaStaff";

    /// <summary>Projectile launch speed.</summary>
    [DataField]
    public float ProjectileSpeed = 15f;
}
