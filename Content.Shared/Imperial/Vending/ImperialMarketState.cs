using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

[Serializable, NetSerializable]
public sealed class ImperialMarketState(string holder, int balance, int launchFee, int teleportFee, List<ImperialMarketEntry> items) : BoundUserInterfaceState
{
    public readonly string Holder = holder;
    public readonly int Balance = balance;
    public readonly int LaunchFee = launchFee;
    public readonly int TeleportFee = teleportFee;
    public readonly List<ImperialMarketEntry> Items = items;
}
