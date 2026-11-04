using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialCustomVendorState(string owner, bool isOwner, int cash, List<ImperialCustomVendorEntry> items) : BoundUserInterfaceState
{
    public readonly string Owner = owner;
    public readonly bool IsOwner = isOwner;
    public readonly int Cash = cash;
    public readonly List<ImperialCustomVendorEntry> Items = items;
}
