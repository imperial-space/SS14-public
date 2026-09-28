using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonBrainDamageComponent : Component
{
    public const float MaxBrainDamage = 200f;
    public const float LowThreshold = 45f;
    public const float HighThreshold = 120f;
    public const float CasterCap = 140f;

    [DataField]
    public float BrainDamage = 0f;

    /// <summary>
    /// Рассудок: 0 — безумие, 150 — максимум. Снижается на 20 при каждом попадании Gate of Mind.
    /// </summary>
    [DataField]
    public float Sanity = 150f;

    [DataField]
    public bool LowThresholdMessageSent = false;

    [DataField]
    public bool HighThresholdMessageSent = false;
}
