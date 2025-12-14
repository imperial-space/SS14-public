using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Plague;

[Prototype("plagueSymptom")]
public sealed partial class MedievalPlagueSymptomPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField(required: true)]
    public string Name { get; } = default!;

    [DataField(required: true)]
    public string Desc { get; } = default!;

    [DataField(required: true)]
    public SpriteSpecifier Icon { get; } = default!;

    [DataField(required: true)]
    public int Cost { get; } = 5;

    [DataField(required: true)]
    public int Tier { get; } = 1;

    [DataField(required: true)]
    public Vector2 Position;

    [DataField]
    public bool StaticCost = false;

    [DataField]
    public string[] Required = Array.Empty<string>();

    [DataField(serverOnly: true)]
    public object? IncubationTargetEvent { get; }

    [DataField(serverOnly: true)]
    public object? TargetEvent { get; }

    [DataField(serverOnly: true)]
    public object? BroadcastEvent { get; }

    [DataField(serverOnly: true)]
    public EntProtoId[] Actions { get; } = Array.Empty<EntProtoId>();

    [DataField]
    public SymptomCategory Category = SymptomCategory.Symptom;

    public int GetCost(SummaryPlagueData data)
    {
        if (StaticCost)
            return Cost;

        return Cost * data.PlagueGhosts;
    }
}

public enum SymptomCategory
{
    Action,
    Symptom,
    Spread
}
