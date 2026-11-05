using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Ответ кандидата на приглашение.
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialDeaconInviteResponseEvent(bool accepted) : EntityEventArgs
{
    public readonly bool Accepted = accepted;
}
