namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Tracks the per-entity cooldown after the Soul potion effect expires.
/// Added/managed by HereticMawedCrucibleSystem.
/// </summary>
[RegisterComponent]
public sealed partial class HereticSoulCooldownComponent : Component
{
    public TimeSpan CooldownEnd = TimeSpan.Zero;
}
