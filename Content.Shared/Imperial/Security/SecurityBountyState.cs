using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class SecurityBountyState : BoundUserInterfaceState
{
    public bool HasPad;
    public bool HasId;
    public string? IdName;
    public string StatusReport = string.Empty;
    public bool Sending;
    public SecurityBountyEntry? Current;
    public string? CurrentProgress;
    public List<SecurityBountyEntry> Options = new();
}
