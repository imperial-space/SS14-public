using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BluespaceTap;

[Serializable, NetSerializable]
public sealed class BluespaceTapProductState(int key, string name, int price)
{
    public readonly int Key = key;
    public readonly string Name = name;
    public readonly int Price = price;
}
