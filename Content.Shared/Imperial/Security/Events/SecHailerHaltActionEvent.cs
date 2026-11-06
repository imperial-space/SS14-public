using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Compli-o-nator и электростул ──────────────────────────────────────────

/// <summary>datum/action/item_action/halt.</summary>
public sealed partial class SecHailerHaltActionEvent : InstantActionEvent;
