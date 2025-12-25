namespace Content.Shared.Imperial.Medieval.SmithingSystem.Behaviours;

[RegisterComponent]
public sealed partial class UpgradeWeaponOnSmithCompleteComponent : Component
{
    [DataField]
    public Dictionary<int, SmithQualityModifiers> ItemQualityTable { get; set; } = new()
    {
        { -12, new SmithQualityModifiers(ItemQuality.Worst, 0.85f) },
        { -8, new SmithQualityModifiers(ItemQuality.ReallyBad, 0.9f) },
        { -2, new SmithQualityModifiers(ItemQuality.Bad, 0.95f) },
        { 2, new SmithQualityModifiers(ItemQuality.Default, 1.05f) },
        { 12, new SmithQualityModifiers(ItemQuality.Good, 1.1f) },
        { 20, new SmithQualityModifiers(ItemQuality.Excellent, 1.2f) },
    };
}
