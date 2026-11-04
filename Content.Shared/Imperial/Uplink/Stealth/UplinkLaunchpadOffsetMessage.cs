using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

[Serializable, NetSerializable]
public sealed class UplinkLaunchpadOffsetMessage(int x, int y) : BoundUserInterfaceMessage
{
    public readonly int X = x;
    public readonly int Y = y;
}
