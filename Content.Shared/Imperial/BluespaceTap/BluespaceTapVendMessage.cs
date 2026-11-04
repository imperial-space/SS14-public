using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

[Serializable, NetSerializable]
public sealed class BluespaceTapVendMessage(int key) : BoundUserInterfaceMessage
{
    public readonly int Key = key;
}
