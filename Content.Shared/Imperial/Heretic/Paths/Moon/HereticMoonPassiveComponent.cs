namespace Content.Shared.Imperial.Heretic.Paths.Moon;

[RegisterComponent]
public sealed partial class HereticMoonPassiveComponent : Component
{
    public TimeSpan TimeOfLastDamage = TimeSpan.Zero;
    public static readonly TimeSpan CombatTimeout = TimeSpan.FromSeconds(5);

    public bool IsInCombat(TimeSpan curTime) => curTime - TimeOfLastDamage < CombatTimeout;
}
