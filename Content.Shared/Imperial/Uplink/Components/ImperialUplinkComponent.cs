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

/// <summary>
/// Одна скидка: позиция, категория распродажи и на сколько ТК снижена цена.
/// </summary>
public sealed class ImperialUplinkSale
{
    public string ListingId = string.Empty;
    public ProtoId<StoreCategoryPrototype> Category;
    public FixedPoint2 Reduction;
    public int Percent;

    /// <summary>Скидка только на одну покупку (ограниченные командные скидки ядерщиков).</summary>
    public bool Limited;

    public bool Spent;
}
