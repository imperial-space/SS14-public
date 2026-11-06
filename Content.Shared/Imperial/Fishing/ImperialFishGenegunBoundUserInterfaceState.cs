using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishGenegunBoundUserInterfaceState(NetEntity fish, List<ImperialFishGenegunTrait> traits) : BoundUserInterfaceState
{
    public readonly NetEntity Fish = fish;
    public readonly List<ImperialFishGenegunTrait> Traits = traits;
}
