using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/component/grillable: на гриле через Time секунд становится Result.</summary>
[RegisterComponent]
public sealed partial class ImperialGrillableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(120f);

    [DataField]
    public bool Positive = true;

    [ViewVariables]
    public float Progress;
}
