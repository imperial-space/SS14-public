using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class SecurityBountyActionMessage(SecurityBountyAction action) : BoundUserInterfaceMessage
{
    public readonly SecurityBountyAction Action = action;
}
