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

    [DataField]
    public TimeSpan DetomatixUseTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan DetomatixUseDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public float DetomatixUseTotalIntensity = 4f;

    [DataField]
    public float DetomatixUseSlope = 2f;

    [DataField]
    public float DetomatixUseMaxTileIntensity = 2f;
}
