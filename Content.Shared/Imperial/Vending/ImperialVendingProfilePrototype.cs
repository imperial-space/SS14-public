using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>
/// Торгомат TG (/obj/machinery/vending/*) для набора SS14 (ID — прототип vendingMachineInventory):
/// цены, отдел оплаты и, если replace, ассортимент TG.
/// </summary>
[Prototype]
public sealed partial class ImperialVendingProfilePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string? TgVendor;

    /// <summary>Бесплатный автомат (all_products_free).</summary>
    [DataField]
    public bool Free;

    /// <summary>default_price.</summary>
    [DataField]
    public int DefaultPrice = 25;

    /// <summary>extra_price.</summary>
    [DataField]
    public int ExtraPrice = 50;

    /// <summary>payment_department: куда идут деньги и чьи сотрудники платят 20%. Пусто — NO_FREEBIES.</summary>
    [DataField]
    public ProtoId<CargoAccountPrototype>? Department;

    /// <summary>Заменить ассортимент на товары TG.</summary>
    [DataField]
    public bool Replace;

    [DataField]
    public List<ImperialVendingProduct> Products = new();
}
