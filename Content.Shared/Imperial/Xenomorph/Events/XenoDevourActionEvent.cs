using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Devour: пожрать того, кого тащишь.</summary>
public sealed partial class XenoDevourActionEvent : XenoEntityTargetActionEvent;
