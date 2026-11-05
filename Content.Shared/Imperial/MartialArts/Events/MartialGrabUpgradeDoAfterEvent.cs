using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

// ─── Do-after ───────────────────────────────────────────────────────────

/// <summary>grippedby(): усиление захвата до шеи и удушения.</summary>
[Serializable, NetSerializable]
public sealed partial class MartialGrabUpgradeDoAfterEvent : SimpleDoAfterEvent;
