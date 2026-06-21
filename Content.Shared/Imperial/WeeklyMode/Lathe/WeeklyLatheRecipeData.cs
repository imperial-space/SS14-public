using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Lathe;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct WeeklyLatheRecipeMaterialData
{
    [DataField]
    public string MaterialId { get; init; } = string.Empty;

    [DataField]
    public int Amount { get; init; }
}

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct WeeklyLatheRecipeData
{
    [DataField]
    public string RecipeId { get; init; } = string.Empty;

    [DataField]
    public string Name { get; init; } = string.Empty;

    [DataField]
    public string Description { get; init; } = string.Empty;

    [DataField]
    public string ResultPrototype { get; init; } = string.Empty;

    [DataField]
    public int ResultAmount { get; init; } = 1;

    [DataField]
    public double ProductionTimeSeconds { get; init; } = 5;

    [DataField]
    public bool ApplyMaterialDiscount { get; init; } = true;

    [DataField]
    public List<WeeklyLatheRecipeMaterialData> Materials { get; init; } = new();

    [DataField]
    public SpriteSpecifier Icon { get; init; } = SpriteSpecifier.Invalid;
}
