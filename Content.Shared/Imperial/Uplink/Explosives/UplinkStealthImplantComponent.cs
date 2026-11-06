using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Имплант S3: картонная коробка-хамелеон по нажатию.</summary>
[RegisterComponent]
public sealed partial class UplinkStealthImplantComponent : Component
{
    [DataField]
    public EntProtoId Box = "StealthBox";
}
