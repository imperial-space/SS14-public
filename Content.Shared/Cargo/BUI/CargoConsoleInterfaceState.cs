using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Cargo.BUI;

[NetSerializable, Serializable]
public sealed class CargoConsoleInterfaceState : BoundUserInterfaceState
{
    public string Name;
    public int Count;
    public int Capacity;
    public NetEntity Station;
    public List<CargoOrderData> Orders;
    public List<ProtoId<CargoProductPrototype>> Products;
    // Imperial Weekly Mode Start
    public List<WeeklyCargoProductData> WeeklyProducts;
    // Imperial Weekly Mode End

    // Imperial Weekly Mode: Original code removed:
    // public CargoConsoleInterfaceState(string name, int count, int capacity, NetEntity station, List<CargoOrderData> orders, List<ProtoId<CargoProductPrototype>> products)
    // Imperial Weekly Mode Start
    public CargoConsoleInterfaceState(
        string name,
        int count,
        int capacity,
        NetEntity station,
        List<CargoOrderData> orders,
        List<ProtoId<CargoProductPrototype>> products,
        List<WeeklyCargoProductData>? weeklyProducts = null)
    // Imperial Weekly Mode End
    {
        Name = name;
        Count = count;
        Capacity = capacity;
        Station = station;
        Orders = orders;
        Products = products;
        // Imperial Weekly Mode
        WeeklyProducts = weeklyProducts ?? new List<WeeklyCargoProductData>();
    }
}
