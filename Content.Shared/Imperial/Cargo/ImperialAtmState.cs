using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialAtmState(string holder, int accountId, int balance, string message) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int AccountId = accountId;
    public readonly int Balance = balance;
    public readonly string Message = message;
}
