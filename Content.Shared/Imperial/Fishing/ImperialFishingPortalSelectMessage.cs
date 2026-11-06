using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishingPortalSelectMessage(string key) : BoundUserInterfaceMessage
{
    public readonly string Key = key;
}
