using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Achievements;

[Prototype]
public sealed partial class AchievementRarityPrototype : IPrototype, IComparable<AchievementRarityPrototype>
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Name = default!;

    [DataField(required: true)]
    public string AccentColor = "#9e9e9e";

    /// <summary>
    /// Lower ownership percentage threshold (inclusive).
    /// Rarity is applied if the percentage is >= MinPercent.
    /// From all matching prototypes, the one with the highest MinPercent is selected
    /// (the highest threshold that still qualifies).
    /// </summary>
    [DataField]
    public float MinPercent = 0f;

    [DataField]
    public int SortOrder = 0;

    public int CompareTo(AchievementRarityPrototype? other)
        => SortOrder.CompareTo(other?.SortOrder ?? 0);
}
