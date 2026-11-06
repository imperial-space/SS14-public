using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Uplink.Components;

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
