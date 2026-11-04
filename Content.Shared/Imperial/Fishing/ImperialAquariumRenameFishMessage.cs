using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumRenameFishMessage(NetEntity fish, string name) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fish = fish;
    public readonly string Name = name;
}
