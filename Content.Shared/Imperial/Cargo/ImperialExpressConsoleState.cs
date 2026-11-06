using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialExpressConsoleState(
    int points,
    bool locked,
    bool usingBeacon,
    bool hasBeacon,
    bool canBeacon,
    bool canBuyBeacon,
    string beaconName,
    string printMessage,
    string message,
    List<ImperialExpressPack> packs) : BoundUserInterfaceState
{
    public readonly int Points = points;
    public readonly bool Locked = locked;
    public readonly bool UsingBeacon = usingBeacon;
    public readonly bool HasBeacon = hasBeacon;
    public readonly bool CanBeacon = canBeacon;
    public readonly bool CanBuyBeacon = canBuyBeacon;
    public readonly string BeaconName = beaconName;
    public readonly string PrintMessage = printMessage;
    public readonly string Message = message;
    public readonly List<ImperialExpressPack> Packs = packs;
}
