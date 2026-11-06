namespace Content.Shared.Imperial.Heretic.Paths.Blade;

/// <summary>
/// Marker for a Blade heretic who has ascended via Maelstrom of Silver:
/// permanent ring of orbiting blades, stun/knockdown immunity, blood-steal on hit.
/// </summary>
[RegisterComponent]
public sealed partial class HereticMaelstromOfSilverComponent : Component
{
    // SS13: add_stun_absorption — tracks absorbed stun seconds; max 45s, recharge 2 min.
    [DataField]
    public TimeSpan StunAbsorbedSeconds = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan? StunAbsorptionRechargeDoneAt = null;

    [DataField]
    public TimeSpan StunAbsorbedSecondsDelay = TimeSpan.FromSeconds(0f);

    [DataField]
    public TimeSpan StunAbsorptionRechargeDoneDelay = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan MaelstromStunnedDelay = TimeSpan.FromMinutes(2);
}
