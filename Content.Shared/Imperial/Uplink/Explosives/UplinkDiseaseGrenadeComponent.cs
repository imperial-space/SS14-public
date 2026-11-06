using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Граната грибкового туберкулёза: заражает всех в облаке.</summary>
[RegisterComponent]
public sealed partial class UplinkDiseaseGrenadeComponent : Component
{
    [DataField(required: true)]
    public string Disease = string.Empty;

    [DataField]
    public float Range = 5f;
}
