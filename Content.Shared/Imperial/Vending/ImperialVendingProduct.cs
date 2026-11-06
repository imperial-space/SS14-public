using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[DataDefinition]
public sealed partial class ImperialVendingProduct
{
    [DataField(required: true)]
    public EntProtoId Id;

    [DataField]
    public uint Amount = 1;

    [DataField]
    public int Price;

    [DataField]
    public ImperialVendingProductType Type = ImperialVendingProductType.Regular;

    /// <summary>Категория из product_categories; пусто — «Товары».</summary>
    [DataField]
    public string? Category;
}
