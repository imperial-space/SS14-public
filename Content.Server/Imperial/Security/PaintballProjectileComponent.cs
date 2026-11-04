using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>projectile/paintball: оставляет пятно краски.</summary>
[RegisterComponent]
public sealed partial class PaintballProjectileComponent : Component
{
    [DataField]
    public EntProtoId Splat = "PaintballSplat";
}
