using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class LaborClaimState : BoundUserInterfaceState
{
    public int IdPoints;
    public string StatusInfo = string.Empty;
    public int UnclaimedPoints;
    public bool CanGoHome;
}
