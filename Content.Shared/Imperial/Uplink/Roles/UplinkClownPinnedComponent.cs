using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Оружие с клоунским бойком.</summary>
[RegisterComponent]
public sealed partial class UplinkClownPinnedComponent : Component
{
    [DataField]
    public bool SelfDestruct;

    [DataField]
    public float ClownPinnedShotTotalIntensity = 30f;

    [DataField]
    public float ClownPinnedShotSlope = 6f;

    [DataField]
    public float ClownPinnedShotMaxTileIntensity = 6f;
}
