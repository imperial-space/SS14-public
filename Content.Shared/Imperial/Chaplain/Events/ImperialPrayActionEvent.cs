using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Chaplain;

/// <summary>
/// Действие «Помолиться»: молитва святого уходит администрации с пометкой капеллана.
/// </summary>
public sealed partial class ImperialPrayActionEvent : InstantActionEvent;
