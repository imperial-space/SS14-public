using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>
/// Эволюция рыбы (/datum/fish_evolution): при размножении или росте вместо своего вида получается новый.
/// </summary>
[Prototype]
public sealed partial class ImperialFishEvolutionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>name: если не задано — название нового вида.</summary>
    [DataField]
    public LocId? Name;

    [DataField]
    public int Probability;

    [DataField(required: true)]
    public EntProtoId NewFish;

    [DataField]
    public float TemperatureMin;

    [DataField]
    public float TemperatureMax = float.PositiveInfinity;

    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> NewTraits = new();

    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> RemovedTraits = new();

    [DataField]
    public LocId? ConditionsNote;

    /// <summary>Особое условие (check_conditions): fedLube, mastodon, chainsawfish, armoredPike, moonfish, dwarfMoonfish.</summary>
    [DataField]
    public string? Condition;

    /// <summary>show_result_on_wiki: в каталоге результат скрыт («???»).</summary>
    [DataField]
    public bool HideResult;
}
