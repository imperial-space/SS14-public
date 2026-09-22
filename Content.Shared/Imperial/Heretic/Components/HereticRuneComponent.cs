namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marks the transmutation rune entity placed by a heretic.
/// </summary>
[RegisterComponent]
public sealed partial class HereticRuneComponent : Component
{
    /// <summary>EntityUid of the heretic who drew this rune.</summary>
    [DataField]
    public EntityUid Caster = EntityUid.Invalid;

    /// <summary>Radius (in tiles) around the rune to search for ritual ingredients.</summary>
    [DataField]
    public float IngredientSearchRadius = 1.5f;
}
