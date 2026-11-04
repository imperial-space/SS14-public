using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialNtPayTransferMessage(int target, int amount) : Content.Shared.CartridgeLoader.CartridgeMessageEvent
{
    public readonly int Target = target;
    public readonly int Amount = amount;
}
