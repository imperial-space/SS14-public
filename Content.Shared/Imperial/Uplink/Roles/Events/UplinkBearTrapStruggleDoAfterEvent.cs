using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

[Serializable, NetSerializable]
public sealed partial class UplinkBearTrapStruggleDoAfterEvent : SimpleDoAfterEvent;
