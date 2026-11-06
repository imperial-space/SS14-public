using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorEntry(NetEntity item, string proto, string name, int price)
{
    public readonly NetEntity Item = item;
    public readonly string Proto = proto;
    public readonly string Name = name;
    public readonly int Price = price;
}
