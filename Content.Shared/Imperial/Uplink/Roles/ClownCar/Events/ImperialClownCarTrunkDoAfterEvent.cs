using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles.ClownCar;

/// <summary>Открыть багажник снаружи (attack_hand).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialClownCarTrunkDoAfterEvent : SimpleDoAfterEvent;
