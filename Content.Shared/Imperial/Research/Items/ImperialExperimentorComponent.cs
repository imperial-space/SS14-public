using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>E.X.P.E.R.I-MENTOR: опыты над предметами с непредсказуемым результатом.</summary>
[RegisterComponent]
public sealed partial class ImperialExperimentorComponent : Component
{
    [DataField]
    public string Slot = "item";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUse;
}
