using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Граната всплывающей турели: турель не стреляет в того, кто выдернул чеку.</summary>
[RegisterComponent]
public sealed partial class UplinkPopupTurretGrenadeComponent : Component
{
    [DataField]
    public EntProtoId Turret = "ImperialPopupTurret";

    [ViewVariables]
    public EntityUid? Owner;
}
