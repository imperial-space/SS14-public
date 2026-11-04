using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

// Do-after события рыбалки: передаются клиенту, поэтому должны лежать в Shared.

[Serializable, NetSerializable]
public sealed partial class ImperialFishingReelDoAfterEvent : SimpleDoAfterEvent;
