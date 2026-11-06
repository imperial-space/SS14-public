using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Paths.Lock.KeyRing;

[Serializable, NetSerializable]
public sealed class HereticAbsorbedCardInfo
{
    public string Name = string.Empty;
    public List<string> AccessTags = new();
}
