using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialAquariumPetFishMessage(NetEntity fish) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fish = fish;
}
