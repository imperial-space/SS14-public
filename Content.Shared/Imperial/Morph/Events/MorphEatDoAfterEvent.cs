using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

// ─── Do-after ───────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed partial class MorphEatDoAfterEvent : SimpleDoAfterEvent;
