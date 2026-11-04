using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/component/bakeable: в духовке через Time секунд становится Result.</summary>
[RegisterComponent]
public sealed partial class ImperialBakeableComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>required_bake_time в секундах.</summary>
    [DataField]
    public float Time = 120f;

    /// <summary>positive_result: хорошая выпечка или горелое.</summary>
    [DataField]
    public bool Positive = true;

    [ViewVariables]
    public float Progress;
}
