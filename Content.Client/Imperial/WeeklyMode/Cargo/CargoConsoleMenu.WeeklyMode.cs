using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Cargo.UI;

public sealed partial class CargoConsoleMenu
{
    // Imperial Weekly Mode Start
    private List<ProductDisplayData>? _cachedProductDisplayData;

    public List<WeeklyCargoProductData> WeeklyProductCatalogue = new();

    private sealed class ProductDisplayData
    {
        public string ProductId = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Category = string.Empty;
        public int Cost;
        public SpriteSpecifier Icon = SpriteSpecifier.Invalid;
        public CargoProductPrototype? Product;
        public WeeklyCargoProductData? WeeklyProduct;
    }

    private static string LocalizeOrLiteral(string value)
    {
        return Loc.TryGetString(value, out var localized)
            ? localized
            : value;
    }

    private List<ProductDisplayData> BuildProductDisplayData()
    {
        var products = new List<ProductDisplayData>();

        foreach (var prototype in ProductPrototypes)
        {
            products.Add(new ProductDisplayData
            {
                ProductId = prototype.ID,
                Name = prototype.Name,
                Description = prototype.Description,
                Category = LocalizeOrLiteral(prototype.Category),
                Cost = prototype.Cost,
                Icon = prototype.Icon,
                Product = prototype,
            });
        }

        foreach (var product in WeeklyProductCatalogue)
        {
            products.Add(new ProductDisplayData
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Description = product.Description,
                Category = LocalizeOrLiteral(product.Category),
                Cost = product.Cost,
                Icon = product.Icon,
                WeeklyProduct = product,
            });
        }

        products.Sort((x, y) =>
            string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase));

        return products;
    }

    private List<ProductDisplayData> GetProductDisplayData()
    {
        return _cachedProductDisplayData ??= BuildProductDisplayData();
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
    // Imperial Weekly Mode End
}
