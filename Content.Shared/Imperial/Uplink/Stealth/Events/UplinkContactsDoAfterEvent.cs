using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

[Serializable, NetSerializable]
public sealed partial class UplinkContactsDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
