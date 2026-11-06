using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ZLevels;

/// <summary>Подняться или опуститься на этаж: призраки, камера ИИ, летящие на джетпаке.</summary>
public sealed partial class ZMoveUpActionEvent : InstantActionEvent;
