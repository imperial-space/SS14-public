using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Trading.Prototypes;
using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Trading;

[Serializable, NetSerializable]
public enum TradingUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TradingUpdateState : BoundUserInterfaceState
{
    public readonly HashSet<Guild> Guilds;

    public readonly int Balance;
    public readonly NetEntity? User;
    public readonly ProtoId<CurrencyPrototype> Currency;

    public TradingUpdateState(HashSet<Guild> guilds, int balance, ProtoId<CurrencyPrototype> currency, NetEntity? user)
    {
        Guilds = guilds;
        Balance = balance;
        Currency = currency;
        User = user;
    }
}

[Serializable, NetSerializable]
public sealed class TradingRequestUpdateInterfaceMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class TradingBuyMessage(GuildTradingItem item) : BoundUserInterfaceMessage
{
    public GuildTradingItem Item = item;
}

[Serializable, NetSerializable]
public sealed class TradingRequestWithdrawMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public TradingRequestWithdrawMessage(int amount)
    {
        Amount = amount;
    }
}
