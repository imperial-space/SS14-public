using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Черта рыбы (/datum/fish_trait). Поведение — в ImperialFishTraitSystem по ID.</summary>
[Prototype]
public sealed partial class ImperialFishTraitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public List<ProtoId<ImperialFishTraitPrototype>> Incompatible = new();

    [DataField]
    public int Inheritability = 50;

    /// <summary>spontaneous_manifest_types: прототип рыбы → шанс в процентах.</summary>
    [DataField]
    public Dictionary<EntProtoId, int> Spontaneous = new();

    /// <summary>fish_whitelist.</summary>
    [DataField]
    public List<EntProtoId>? Whitelist;

    [DataField]
    public int AddedDifficulty;

    /// <summary>reagents_to_add: реагенты в съедобной рыбе (умножаются на число укусов).</summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, float> Reagents = new();
}
