using Content.Client.Cargo;
using Content.Client.Cargo.UI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.WeeklyMode.Cargo;

[DataDefinition]
public sealed partial class ProductDisplayData
{
    [DataField]
    public string ProductId = string.Empty;
    [DataField]
    public string Name = string.Empty;
    [DataField]
    public string Description = string.Empty;
    [DataField]
    public string Category = string.Empty;
    [DataField]
    public int Cost;
    [DataField]
    public SpriteSpecifier Icon = SpriteSpecifier.Invalid;
    [DataField]
    public CargoProductPrototype? Product;
    [DataField]
    public WeeklyCargoProductData? WeeklyProduct;
}
