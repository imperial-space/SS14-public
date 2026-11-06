using Robust.Shared.Map;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticCrucibleEffectComponent : Component
{
    [ViewVariables]
    public HereticCruciblePotionType ActivePotion;
    [ViewVariables]
    public TimeSpan EndTime;

    // Soul potion: wall phase
    [ViewVariables]
    public EntityCoordinates? OriginalPosition;
    [ViewVariables]
    public List<(string Id, bool Hard, int Layer, int Mask)> FixtureStates = new();
    // Cooldown to apply to the player after the Soul effect ends
    [ViewVariables]
    public TimeSpan SoulCooldown = TimeSpan.FromSeconds(120f);

    // Clarity potion: original FOV value to restore
    [ViewVariables]
    public bool OriginalFov = true;

    // Marshal potion: heal on ticks
    [ViewVariables]
    public TimeSpan NextHealTick;

    [DataField]
    public TimeSpan HealTickDelay = TimeSpan.FromSeconds(1);
}
