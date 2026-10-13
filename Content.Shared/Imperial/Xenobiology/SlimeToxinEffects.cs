using Content.Shared.EntityEffects;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>
/// reagent/mutationtoxin: после cycles_to_turn циклов метаболизма меняет расу существа.
/// </summary>
public sealed partial class SlimeSpeciesMutation : EntityEffectBase<SlimeSpeciesMutation>
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> Polymorph;

    /// <summary>cycles_to_turn = 20 по 2 с в SS13 (метаболизм SS14 — раз в секунду).</summary>
    [DataField]
    public int Cycles = 40;
}

/// <summary>reagent/aslimetoxin: заражение disease/transformation/slime.</summary>
public sealed partial class SlimeTransformationExposure : EntityEffectBase<SlimeTransformationExposure>;
