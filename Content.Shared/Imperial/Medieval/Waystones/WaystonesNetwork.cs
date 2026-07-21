using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Waystones;

[Serializable, NetSerializable]
public enum WaystoneUiKey : byte
{
    Key,
    AdminKey
}

[Serializable, NetSerializable]
public sealed class WaystoneUpdateState : BoundUserInterfaceState
{
    public readonly List<WaystoneInfo> Waystones;
    public WaystoneUpdateState(List<WaystoneInfo> waystones) => Waystones = waystones;
}

[Serializable, NetSerializable]
public struct WaystoneInfo
{
    public NetEntity Entity;
    public string Name;
    public int DeparturePrice;
    public int ArrivalPrice;
    public bool IsEnable;

    public WaystoneInfo(NetEntity entity, string name, int departurePrice, int arrivalPrice, bool isEnable)
    {
        Entity = entity;
        Name = name;
        DeparturePrice = departurePrice;
        ArrivalPrice = arrivalPrice;
        IsEnable = isEnable;
    }
}

[Serializable, NetSerializable]
public sealed class WaystoneSelectMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity TargetWaystone;
    public WaystoneSelectMessage(NetEntity targetWaystone) => TargetWaystone = targetWaystone;
}

[Serializable, NetSerializable]
public sealed partial class WaystoneTeleportDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed class WaystoneStateMessage : BoundUserInterfaceMessage
{
    public int DeparturePrice;
    public int ArrivalPrice;
    public bool State;

    public WaystoneStateMessage(int departurePrice, int arrivalPrice, bool state)
    {
        DeparturePrice = departurePrice;
        ArrivalPrice = arrivalPrice;
        State = state;
    }
}
