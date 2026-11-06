using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Uplink.Components;

/// <summary>
/// Метка аплинка, каталог которого заменён на каталог TG. Хранит распродажи этого аплинка.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialUplinkComponent : Component
{
    [ViewVariables]
    public List<ImperialUplinkSale> Sales = new();

    [ViewVariables]
    public bool SalesRolled;
}
