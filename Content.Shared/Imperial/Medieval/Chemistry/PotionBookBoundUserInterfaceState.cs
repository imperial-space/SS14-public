using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Chemistry;

[Serializable, NetSerializable]
public sealed class PotionBookUserInterfaceState : BoundUserInterfaceState
{
    public List<string> Ids = new();
}
