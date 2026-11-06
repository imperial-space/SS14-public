using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Окно пульта телепада (launchpad_remote TG).</summary>
[Serializable, NetSerializable]
public sealed class UplinkLaunchpadState(bool linked, bool deployed, int x, int y, int range) : BoundUserInterfaceState
{
    public readonly bool Linked = linked;
    public readonly bool Deployed = deployed;
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int Range = range;
}
