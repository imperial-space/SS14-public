using Content.Shared.FixedPoint;

namespace Content.Shared.Imperial.Heretic.Rituals;

/// <summary>
/// Marks the transmutation rune entity placed by a heretic.
/// </summary>
[RegisterComponent]
public sealed partial class HereticRuneComponent : Component
{
    /// <summary>EntityUid of the heretic who drew this rune.</summary>
    [DataField]
    public EntityUid? Caster;

    /// <summary>Radius (in tiles) around the rune to search for ritual ingredients.</summary>
    [DataField]
    public float IngredientSearchRadius = 1.5f;

    [DataField]
    public TimeSpan RitualSelectedDelay = TimeSpan.FromMilliseconds(5000);

    [DataField]
    public FixedPoint2 RitualSelectedDamage = 125;

    [DataField]
    public int RitualSelectedAmount = 4;
}
