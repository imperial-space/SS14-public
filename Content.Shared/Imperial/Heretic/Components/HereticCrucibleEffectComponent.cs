using Robust.Shared.Map;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticCrucibleEffectComponent : Component
{
    public HereticCruciblePotionType ActivePotion;
    public TimeSpan EndTime;

    // Soul potion: wall phase
    public EntityCoordinates? OriginalPosition;
    public List<(string Id, bool Hard, int Layer, int Mask)> FixtureStates = new();
    // Cooldown to apply to the player after the Soul effect ends
    public float SoulCooldown = 120f;

    // Clarity potion: original FOV value to restore
    public bool OriginalFov = true;

    // Marshal potion: heal on ticks
    public TimeSpan NextHealTick;
}
