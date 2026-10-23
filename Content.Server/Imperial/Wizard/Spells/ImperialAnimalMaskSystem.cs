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

public sealed class ImperialAnimalMaskSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialAnimalMaskComponent, InventoryRelayedEvent<TransformSpeechEvent>>(OnSpeech);
    }

    /// <summary>handle_speech(): фраза целиком заменяется звуком животного.</summary>
    private void OnSpeech(Entity<ImperialAnimalMaskComponent> ent, ref InventoryRelayedEvent<TransformSpeechEvent> args)
    {
        if (ent.Comp.Sounds.Count == 0)
            return;

        var alt = ent.Comp.AltSounds.Count > 0 && _random.Prob(ent.Comp.AltProbability);
        args.Args.Message = _random.Pick(alt ? ent.Comp.AltSounds : ent.Comp.Sounds);
    }
}
