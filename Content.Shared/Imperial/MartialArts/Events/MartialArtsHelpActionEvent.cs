using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.MartialArts;

// ─── Способности ────────────────────────────────────────────────────────

/// <summary>datum/action/swap_arts: ЛКМ — справка, второе нажатие с зажатым Alt недоступно, поэтому смена стиля отдельной кнопкой.</summary>
public sealed partial class MartialArtsHelpActionEvent : InstantActionEvent;
