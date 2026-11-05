using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Heretic.Core;

// ─── DoAfter events ──────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed partial class DrawHereticRuneDoAfterEvent : SimpleDoAfterEvent { }
