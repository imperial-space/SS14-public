using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishGenegunExtractMessage(NetEntity fish, string trait) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fish = fish;
    public readonly string Trait = trait;
}
