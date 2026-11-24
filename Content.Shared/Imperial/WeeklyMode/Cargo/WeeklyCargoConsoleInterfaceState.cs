using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.BUI;

[NetSerializable, Serializable]
public sealed class WeeklyCargoConsoleInterfaceState : BoundUserInterfaceState
{
    public string Name;
    public int Count;
    public int Capacity;
    public NetEntity Station;
    public List<CargoOrderData> Orders;
    public List<ProtoId<CargoProductPrototype>> Products;
    public List<WeeklyCargoProductData> WeeklyProducts;

    public WeeklyCargoConsoleInterfaceState(
        string name,
        int count,
        int capacity,
        NetEntity station,
        List<CargoOrderData> orders,
        List<ProtoId<CargoProductPrototype>> products,
        List<WeeklyCargoProductData>? weeklyProducts = null)
    {
        Name = name;
        Count = count;
        Capacity = capacity;
        Station = station;
        Orders = orders;
        Products = products;
        WeeklyProducts = weeklyProducts ?? new List<WeeklyCargoProductData>();
    }
}
