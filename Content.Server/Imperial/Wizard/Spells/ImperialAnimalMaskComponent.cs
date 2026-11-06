using Content.Shared.Chat;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/item/clothing/mask/animal: вместо речи — звуки животного.</summary>
[RegisterComponent]
public sealed partial class ImperialAnimalMaskComponent : Component
{
    /// <summary>animal_sounds.</summary>
    [DataField(required: true)]
    public List<string> Sounds = new();

    /// <summary>animal_sounds_alt.</summary>
    [DataField]
    public List<string> AltSounds = new();

    /// <summary>animal_sounds_alt_probability (0–1).</summary>
    [DataField]
    public float AltProbability;
}
