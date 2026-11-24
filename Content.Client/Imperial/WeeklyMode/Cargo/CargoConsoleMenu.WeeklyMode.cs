using Content.Client.Cargo;
using Content.Client.Cargo.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.WeeklyMode.Cargo;

public sealed partial class WeeklyCargoConsoleMenu
{
    private List<ProductDisplayData>? _cachedProductDisplayData;

    public List<WeeklyCargoProductData> WeeklyProductCatalogue = new();

    private List<ProductDisplayData> GetProductDisplayData()
    {
        return _cachedProductDisplayData ??= WeeklyCargoProductDisplayHelper.Build(ProductPrototypes, WeeklyProductCatalogue);
    }

    public void InvalidateProductCache()
    {
        _cachedProductDisplayData = null;
    }

    public bool TryGetProductDisplayData(string productId, out string name, out string description, out int cost)
    {
        foreach (var product in GetProductDisplayData())
        {
            if (!string.Equals(product.ProductId, productId, StringComparison.Ordinal))
                continue;

            name = product.Name;
            description = product.Description;
            cost = product.Cost;
            return true;
        }

        name = string.Empty;
        description = string.Empty;
        cost = 0;
        return false;
    }
}
