using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.ShipBuyTerminal;

[Serializable, NetSerializable]
public enum ShipBuyTerminalUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class ShipBuyTerminalUpdateState : BoundUserInterfaceState
{
    public readonly int Balance;
    public readonly List<string> GridOfferIds;
    public readonly string Currency;

    public ShipBuyTerminalUpdateState(int balance, List<string> gridOfferIds, string currency)
    {
        Balance = balance;
        GridOfferIds = gridOfferIds;
        Currency = currency;
    }
}

[Serializable, NetSerializable]
public sealed class ShipBuyTerminalBuyMessage : BoundUserInterfaceMessage
{
    public int OfferIndex;

    public ShipBuyTerminalBuyMessage(int offerIndex)
    {
        OfferIndex = offerIndex;
    }
}

[Serializable, NetSerializable]
public sealed class ShipBuyTerminalWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public ShipBuyTerminalWithdrawMessage(int amount)
    {
        Amount = amount;
    }
}
