using Content.Server.Damage.Systems;
using Content.Server.Traits.Assorted;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Jittering;
using Content.Shared.StatusEffect;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMadnessRuneSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly ParacusiaSystem _paracusia = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMadnessRuneComponent, StepTriggerAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<HereticMadnessRuneComponent, StepTriggeredOnEvent>(OnTriggered);
    }

    private void OnAttempt(Entity<HereticMadnessRuneComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = !HasComp<HereticComponent>(args.Tripper);
    }

    private void OnTriggered(Entity<HereticMadnessRuneComponent> ent, ref StepTriggeredOnEvent args)
    {
        var target = args.Tripper;

        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
            target, TemporaryBlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(5), true);

        _statusEffects.TryAddStatusEffect(
            target, "Stutter", TimeSpan.FromSeconds(10), true, "StutteringAccentComponent");

        if (EnsureComp<ParacusiaComponent>(target, out var paracusia))
        {
            _paracusia.SetSounds(target, new SoundCollectionSpecifier("Paracusia"), paracusia);
            _paracusia.SetTime(target, 3f, 10f, paracusia);
            _paracusia.SetDistance(target, 5f);
        }

        Timer.Spawn(15000, () =>
        {
            if (Exists(target))
                RemComp<ParacusiaComponent>(target);
        });

        EnsureComp<JitteringComponent>(target);

        Timer.Spawn(10000, () =>
        {
            if (Exists(target))
                RemComp<JitteringComponent>(target);
        });

        _stamina.TakeStaminaDamage(target, 50f, visual: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/blind.ogg"), target);
        var coords = Transform(ent.Owner).Coordinates;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/runebreak.ogg"), coords);
        QueueDel(ent.Owner);
    }
}
