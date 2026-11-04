using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Gps;

[Serializable, NetSerializable]
public sealed class ImperialGpsRenameMessage(string tag) : BoundUserInterfaceMessage
{
    public string Tag = tag;
}
