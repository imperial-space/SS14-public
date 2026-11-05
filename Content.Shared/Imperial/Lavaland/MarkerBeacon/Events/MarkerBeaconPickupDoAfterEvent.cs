using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.MarkerBeacon;

[Serializable, NetSerializable]
public sealed partial class MarkerBeaconPickupDoAfterEvent : SimpleDoAfterEvent;
