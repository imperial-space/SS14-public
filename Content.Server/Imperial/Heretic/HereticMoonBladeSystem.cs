using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMoonBladeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem            _audio          = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage    = default!;
    [Dependency] private readonly HereticStatusEffectsSystem   _hereticEffects = default!;
    [Dependency] private readonly MobStateSystem               _mobs           = default!;
    [Dependency] private readonly IRobustRandom                _random         = default!;

    private static readonly SoundPathSpecifier[] LaughSounds =
    [
        new("/Audio/Voice/Human/manlaugh1.ogg"),
        new("/Audio/Voice/Human/manlaugh2.ogg"),
        new("/Audio/Voice/Human/womanlaugh.ogg"),
    ];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonBladeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, HereticMoonBladeComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;
        if (!TryComp<HereticComponent>(args.User, out _))
            return;

        foreach (var target in args.HitEntities)
        {
            if (HasComp<HereticComponent>(target))
                continue;
            if (!_mobs.IsAlive(target))
                continue;

            if (!TryComp<HereticMoonBrainDamageComponent>(target, out var brainComp))
                brainComp = AddComp<HereticMoonBrainDamageComponent>(target);

            brainComp.Sanity = Math.Max(0f, brainComp.Sanity - 10f);

            var brainDmg = brainComp.Sanity >= 75f ? 10f : 25f;
            _brainDamage.AddBrainDamage(target, brainDmg);

            _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(10));
            _audio.PlayPvs(_random.Pick(LaughSounds), args.User);
        }
    }
}
