using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marker for a Blade heretic who has ascended via Maelstrom of Silver:
/// permanent ring of orbiting blades, stun/knockdown immunity, blood-steal on hit.
/// </summary>
[RegisterComponent]
public sealed partial class HereticMaelstromOfSilverComponent : Component
{
    // SS13: add_stun_absorption — tracks absorbed stun seconds; max 45s, recharge 2 min.
    [DataField]
    public float StunAbsorbedSeconds = 0f;

    [DataField]
    public TimeSpan? StunAbsorptionRechargeDoneAt = null;
}
