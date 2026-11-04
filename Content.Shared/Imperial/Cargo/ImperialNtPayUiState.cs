using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

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
