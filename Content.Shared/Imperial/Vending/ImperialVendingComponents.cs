using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

public enum ImperialVendingProductType : byte
{
    /// <summary>products.</summary>
    Regular,

    /// <summary>contraband: видна после взлома проводов.</summary>
    Contraband,

    /// <summary>premium: всегда видна, стоит дороже.</summary>
    Premium,
}

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

/// <summary>Цены торгомата для окна покупки.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialVendingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Free;

    /// <summary>Цена по ключу «тип:прототип».</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Prices = new();

    [DataField, AutoNetworkedField]
    public HashSet<string> Premium = new();

    /// <summary>Категория товара по ключу.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, string> Categories = new();

    [DataField, AutoNetworkedField]
    public ProtoId<CargoAccountPrototype>? Department;

    [DataField, AutoNetworkedField]
    public int DefaultPrice = 25;

    [DataField, AutoNetworkedField]
    public int ExtraPrice = 50;

    public static string Key(InventoryType type, string id) => $"{(byte) type}:{id}";

    public int PriceOf(InventoryType type, string id)
    {
        if (Free)
            return 0;
        if (Prices.TryGetValue(Key(type, id), out var price))
            return price;

        return type == InventoryType.Regular ? DefaultPrice : ExtraPrice;
    }
}

[Serializable, NetSerializable]
public sealed class ImperialVendingBuyMessage(InventoryType type, string id) : BoundUserInterfaceMessage
{
    public readonly InventoryType Type = type;
    public readonly string Id = id;
}

/// <summary>ui_data торгомата: покупатель, деньги (счёт и наличные), отдел и реклама.</summary>
[Serializable, NetSerializable]
public sealed class ImperialVendingUserState(bool hasUser, string holder, string job, int cash, string? department, string ad, bool free) : BoundUserInterfaceState
{
    public readonly bool HasUser = hasUser;
    public readonly string Holder = holder;
    public readonly string Job = job;
    public readonly int Cash = cash;
    public readonly string? Department = department;
    public readonly string Ad = ad;
    public readonly bool Free = free;
}

/// <summary>soil_sack TG: высыпать грядку на пол (тратится целиком).</summary>
[RegisterComponent]
public sealed partial class ImperialSoilSackComponent : Component
{
    [DataField]
    public EntProtoId Soil = "hydroponicsSoil";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public Robust.Shared.Audio.SoundSpecifier? Sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    /// <summary>Клетка, выбранная при начале высыпания.</summary>
    [ViewVariables]
    public Robust.Shared.Map.NetCoordinates? Target;
}

[Serializable, NetSerializable]
public sealed partial class ImperialSoilSackDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
