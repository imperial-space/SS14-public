using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.GibtoniteRock;

/// <summary>
/// Marker component for projectiles that defuse active gibtonite rocks on hit
/// instead of triggering an explosion.
/// </summary>
[RegisterComponent]
public sealed partial class GibtoniteDefuserProjectileComponent : Component
{
}
