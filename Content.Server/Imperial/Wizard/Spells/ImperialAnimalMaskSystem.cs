using Content.Shared.Chat;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

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
