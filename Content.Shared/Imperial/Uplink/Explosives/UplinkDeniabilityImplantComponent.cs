using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Имплант тактического отрицания: без крита, но через 10 секунд после «крита» — взрыв.</summary>
[RegisterComponent]
public sealed partial class UplinkDeniabilityImplantComponent : Component
{
    [ViewVariables]
    public FixedPoint2? OriginalCrit;

    [ViewVariables]
    public TimeSpan? DetonateAt;

    [DataField]
    public TimeSpan DetonateDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public float UpdateImplantsTotalIntensity = 120f;

    [DataField]
    public float UpdateImplantsSlope = 5f;

    [DataField]
    public float UpdateImplantsMaxTileIntensity = 15f;
}
