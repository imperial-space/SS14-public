using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed partial class ImperialAquariumUpgradeDoAfterEvent : SimpleDoAfterEvent;
