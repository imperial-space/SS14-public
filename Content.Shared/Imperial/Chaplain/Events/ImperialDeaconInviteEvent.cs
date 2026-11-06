using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Приглашение вступить в религию станции перед посвящением в дьяконы (invite_deacon из SS13).
/// </summary>
[Serializable, NetSerializable]
public sealed class ImperialDeaconInviteEvent(string deity) : EntityEventArgs
{
    public readonly string Deity = deity;
}
