using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class SecurityBountyEntry
{
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Reward;
}
