using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class BloodBrotherSelectionEuiState(string targetName, List<BloodBrotherSelectablePlayer> players) : EuiStateBase
{
    public readonly string TargetName = targetName;
    public readonly List<BloodBrotherSelectablePlayer> Players = players;
}

[Serializable, NetSerializable]
public sealed class BloodBrotherSelectionChoiceMessage(NetUserId userId) : EuiMessageBase
{
    public readonly NetUserId UserId = userId;
}

[Serializable, NetSerializable]
public sealed class BloodBrotherSelectablePlayer(NetUserId userId, string name)
{
    public readonly NetUserId UserId = userId;
    public readonly string Name = name;
}