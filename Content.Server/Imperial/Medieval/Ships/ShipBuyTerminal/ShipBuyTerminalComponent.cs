using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Ships.ShipBuyTerminal;

[RegisterComponent]
public sealed partial class ShipBuyTerminalComponent : Component
{
    [DataField]
    public int Balance;

    [DataField]
    public float GlobalOffset = 1f;

    [DataField]
    public float GlobalOffsetAngle;

    [DataField]
    public float GlobalGridAngle;

    [DataField]
    public ProtoId<CurrencyPrototype> Currency = "Revent";
}
