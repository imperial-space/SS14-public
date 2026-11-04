using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialPrivateOrderState(string holder, int balance, string message, List<ImperialExpressPack> packs) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int Balance = balance;
    public readonly string Message = message;
    public readonly List<ImperialExpressPack> Packs = packs;
}
