using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticStunRuneSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticStunRuneComponent, StepTriggerAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<HereticStunRuneComponent, StepTriggeredOnEvent>(OnTriggered);
    }

    private void OnAttempt(Entity<HereticStunRuneComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = !HasComp<HereticComponent>(args.Tripper);
    }

    private void OnTriggered(Entity<HereticStunRuneComponent> ent, ref StepTriggeredOnEvent args)
    {
        var target = args.Tripper;
        _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(5));
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Blunt"] = FixedPoint2.New(15);
        _damage.TryChangeDamage(target, dmg, ignoreResistances: false);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_misc_demon_attack1.ogg"), target);
        var coords = Transform(ent.Owner).Coordinates;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/runebreak.ogg"), coords);
        QueueDel(ent.Owner);
    }
}
