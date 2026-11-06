namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticCruciblePotionComponent : Component
{
    [DataField(required: true)]
    public HereticCruciblePotionType PotionType;

    // Seconds the player must hold the potion before the effect triggers
    [DataField]
    public TimeSpan DrinkDelay = TimeSpan.FromSeconds(1f);

    // Cooldown to apply to the player after Soul effect ends (seconds)
    [DataField]
    public TimeSpan SoulCooldown = TimeSpan.FromSeconds(120f);

    // Crucible that produced this potion; used to read SoulCooldown at drink time
    [ViewVariables]
    public EntityUid? SourceCrucible;
}
