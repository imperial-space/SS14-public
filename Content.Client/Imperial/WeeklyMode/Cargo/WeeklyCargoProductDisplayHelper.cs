using Content.Client.Cargo;
using Content.Client.Cargo.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;

namespace Content.Client.Imperial.WeeklyMode.Cargo;

public static class WeeklyCargoProductDisplayHelper
{
    private static string LocalizeOrLiteral(string value)
    {
        return Loc.TryGetString(value, out var localized)
            ? localized
            : value;
    }

    public static List<ProductDisplayData> Build(IEnumerable<CargoProductPrototype> prototypes, IEnumerable<WeeklyCargoProductData> weeklyProducts)
    {
        var products = new List<ProductDisplayData>();

        foreach (var prototype in prototypes)
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

        foreach (var product in weeklyProducts)
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

}
