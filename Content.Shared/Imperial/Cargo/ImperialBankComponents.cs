using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>registered_account у ID-карты: номер личного банковского счёта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialBankCardComponent : Component
{
    [DataField, AutoNetworkedField]
    public int AccountId;
}

/// <summary>/obj/structure/closet/crate/secure/owned и lockbox/order: открыть может только владелец счёта.</summary>
[RegisterComponent]
public sealed partial class ImperialOwnedLockComponent : Component
{
    [DataField]
    public int AccountId;

    [DataField]
    public string Holder = string.Empty;
}

/// <summary>Консоль карго, у которой есть личные заказы (self_paid).</summary>
[RegisterComponent]
public sealed partial class ImperialPrivateOrderConsoleComponent : Component
{
    [ViewVariables]
    public TimeSpan NextOrder;
}

[Serializable, NetSerializable]
public enum ImperialPrivateOrderUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialPrivateOrderState(string holder, int balance, string message, List<ImperialExpressPack> packs) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int Balance = balance;
    public readonly string Message = message;
    public readonly List<ImperialExpressPack> Packs = packs;
}

[Serializable, NetSerializable]
public sealed class ImperialPrivateOrderBuyMessage(string pack) : BoundUserInterfaceMessage
{
    public readonly string Pack = pack;
}

/// <summary>Ящик или кейс набора из каталога TG.</summary>
[RegisterComponent]
public sealed partial class ImperialCargoPackComponent : Component;

/// <summary>Приложение NT Pay в КПК: личный счёт, история операций, переводы (pay_token TG).</summary>
[RegisterComponent]
public sealed partial class ImperialNtPayCartridgeComponent : Component
{
    /// <summary>Счёт владельца КПК; 0 — берётся счёт ID-карты в КПК.</summary>
    [DataField]
    public int AccountId;
}

[Serializable, NetSerializable]
public sealed class ImperialNtPayUiState(string holder, int accountId, int balance, int debt, List<string> history, string message) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int AccountId = accountId;
    public readonly int Balance = balance;
    public readonly int Debt = debt;
    public readonly List<string> History = history;
    public readonly string Message = message;
}

[Serializable, NetSerializable]
public sealed class ImperialNtPayTransferMessage(int target, int amount) : Content.Shared.CartridgeLoader.CartridgeMessageEvent
{
    public readonly int Target = target;
    public readonly int Amount = amount;
}

/// <summary>Банкомат: снятие и внесение наличных на личный счёт.</summary>
[RegisterComponent]
public sealed partial class ImperialAtmComponent : Component;

[Serializable, NetSerializable]
public enum ImperialAtmUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialAtmState(string holder, int accountId, int balance, string message) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int AccountId = accountId;
    public readonly int Balance = balance;
    public readonly string Message = message;
}

[Serializable, NetSerializable]
public sealed class ImperialAtmWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}
