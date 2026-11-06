using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// projectile/kinetic: урон ×pressure_decrease, если давление на клетке выше LAVALAND_EQUIPMENT_EFFECT_PRESSURE.
/// </summary>
[RegisterComponent]
public sealed partial class KineticProjectileComponent : Component
{
    [DataField]
    public float PressureDecrease = 0.25f;

    /// <summary>LAVALAND_EQUIPMENT_EFFECT_PRESSURE, кПа.</summary>
    [DataField]
    public float PressureThreshold = 50f;

    /// <summary>
    /// Давление планетной атмосферы Лаваленда выше 50 кПа, поэтому на карте Лаваленда
    /// «низким» считается всё, что не выше этого значения (наружный воздух планеты).
    /// </summary>
    [DataField]
    public float LavalandPressure = 85f;

    [ViewVariables]
    public bool PressureDecreaseActive;

    [ViewVariables]
    public bool Struck;

    [ViewVariables]
    public EntityUid? Gun;

    [ViewVariables]
    public EntityUid? Firer;
}
