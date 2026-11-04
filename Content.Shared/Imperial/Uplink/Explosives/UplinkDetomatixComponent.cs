using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Диск Детоматикс: подрывает КПК членов экипажа.</summary>
[RegisterComponent]
public sealed partial class UplinkDetomatixComponent : Component
{
    [DataField]
    public int Charges = 4;
}
