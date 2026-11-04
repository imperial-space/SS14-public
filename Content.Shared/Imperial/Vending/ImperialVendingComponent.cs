using Content.Shared.Cargo.Prototypes;
using Content.Shared.VendingMachines;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

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

    /// <summary>SSeconomy.inflation_value: множитель цен (обвал рынка).</summary>
    [DataField, AutoNetworkedField]
    public float Inflation = 1f;

    public static string Key(InventoryType type, string id) => $"{(byte) type}:{id}";

    public int PriceOf(InventoryType type, string id)
    {
        if (Free)
            return 0;
        var basePrice = Prices.TryGetValue(Key(type, id), out var price)
            ? price
            : type == InventoryType.Regular ? DefaultPrice : ExtraPrice;
        return Inflation == 1f ? basePrice : (int) Math.Ceiling(basePrice * Inflation);
    }
}
