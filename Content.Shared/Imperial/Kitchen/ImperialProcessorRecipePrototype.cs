using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/food_processor_process.</summary>
[Prototype]
public sealed partial class ImperialProcessorRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Input;

    [DataField(required: true)]
    public EntProtoId Output;

    /// <summary>multiplier.</summary>
    [DataField]
    public int Multiplier = 1;

    /// <summary>time в секундах.</summary>
    [DataField]
    public float Time = 4f;
}
