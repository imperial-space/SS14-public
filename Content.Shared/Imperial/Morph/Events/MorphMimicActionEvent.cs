using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

// ─── Способности ────────────────────────────────────────────────────────

/// <summary>spell/mimic/morph: по цели — запомнить облик, по себе — выбрать облик.</summary>
public sealed partial class MorphMimicActionEvent : EntityTargetActionEvent;
