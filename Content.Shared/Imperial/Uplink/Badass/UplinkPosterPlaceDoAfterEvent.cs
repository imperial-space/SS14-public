using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

[Serializable, NetSerializable]
public sealed partial class UplinkPosterPlaceDoAfterEvent : SimpleDoAfterEvent;
