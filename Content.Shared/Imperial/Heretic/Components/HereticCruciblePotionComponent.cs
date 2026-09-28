using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticCruciblePotionComponent : Component
{
    [DataField(required: true)]
    public HereticCruciblePotionType PotionType;

    // Seconds the player must hold the potion before the effect triggers
    [DataField]
    public float DrinkDelay = 1f;

    // Cooldown to apply to the player after Soul effect ends (seconds)
    [DataField]
    public float SoulCooldown = 120f;

    // Crucible that produced this potion; used to read SoulCooldown at drink time
    public EntityUid? SourceCrucible;
}

[Serializable, NetSerializable]
public sealed partial class HereticCruciblePotionDoAfterEvent : SimpleDoAfterEvent
{
}
