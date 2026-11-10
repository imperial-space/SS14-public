using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Cargo;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct WeeklyCargoProductData
{
    [DataField]
    public string ProductId { get; init; } = string.Empty;

    [DataField]
    public string Name { get; init; } = string.Empty;

    [DataField]
    public string Description { get; init; } = string.Empty;

    [DataField]
    public string Category { get; init; } = string.Empty;

    [DataField]
    public int Cost { get; init; }

    [DataField]
    public bool Boxed { get; init; }

    [DataField]
    public int Amount { get; init; } = 1;

    [DataField]
    public string ItemPrototype { get; init; } = string.Empty;

    [DataField]
    public SpriteSpecifier Icon { get; init; } = SpriteSpecifier.Invalid;
}
