using Content.Shared.Damage;
using Content.Shared.DoAfter;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class ChargingRuneExplosionComponent : Component
{
    [DataField]
    public float Time = 25f;

    public DoAfterId? DoAfter;

    public TimeSpan NextCheck = TimeSpan.Zero;
}
