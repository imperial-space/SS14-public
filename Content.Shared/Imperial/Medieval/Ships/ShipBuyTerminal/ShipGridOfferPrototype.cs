using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Ships.ShipBuyTerminal;

[Prototype("shipGridOffer")]
public sealed class ShipGridOfferPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string DisplayName = string.Empty;

    [DataField]
    public string DisplayDescription = string.Empty;

    [DataField]
    public string GridPath = string.Empty;

    [DataField]
    public int Cost;

    [DataField]
    public float LocalOffset;

    [DataField]
    public float LocalOffsetAngle;

    [DataField]
    public float GridAngle;
}
