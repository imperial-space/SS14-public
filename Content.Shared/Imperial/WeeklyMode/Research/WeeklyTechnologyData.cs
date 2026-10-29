using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Research;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct WeeklyTechnologyData
{
    [DataField]
    public string TechnologyId { get; init; } = string.Empty;

    [DataField]
    public string Name { get; init; } = string.Empty;

    [DataField]
    public string Branch { get; init; } = string.Empty;

    [DataField]
    public int Cost { get; init; }

    [DataField]
    public int Tier { get; init; } = 1;

    [DataField]
    public List<string> RecipeIds { get; init; } = new();

    [DataField]
    public SpriteSpecifier Icon { get; init; } = SpriteSpecifier.Invalid;
}
