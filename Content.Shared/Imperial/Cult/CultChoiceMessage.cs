using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed class CultChoiceMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
}
