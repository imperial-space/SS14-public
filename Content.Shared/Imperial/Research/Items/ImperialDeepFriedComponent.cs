using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Отмечает обжаренный предмет, чтобы не жарить его повторно.</summary>
[RegisterComponent]
public sealed partial class ImperialDeepFriedComponent : Component;
