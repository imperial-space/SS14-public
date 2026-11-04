using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Имплант-ревивер: лечит и реанимирует, пока носитель без сознания.</summary>
[RegisterComponent]
public sealed partial class UplinkReviverImplantComponent : Component
{
    [ViewVariables]
    public bool Reviving;

    [ViewVariables]
    public float ReviveCost;

    [ViewVariables]
    public TimeSpan ReadyAt;

    [ViewVariables]
    public TimeSpan NextHeal;

    [ViewVariables]
    public TimeSpan DefibAt;
}
