using Content.Server.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Implants;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticEtherFlaskSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticEtherFlaskComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HereticEtherFlaskSleepingComponent, SleepStateChangedEvent>(OnWakeUp);
    }

    private void OnUseInHand(EntityUid uid, HereticEtherFlaskComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        if (!HasComp<HereticComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("heretic-ether-flask-not-heretic"), user, user, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        _damage.SetAllDamage(user, 0);
        if (TryComp<BloodstreamComponent>(user, out var bloodstreamComp))
            _bloodstream.TryModifyBleedAmount(user, -bloodstreamComp.BleedAmount);
        _implants.WipeImplants(user);

        var sleep = EnsureComp<SleepingComponent>(user);
        sleep.WakeThreshold = FixedPoint2.New(100);
        sleep.Cooldown = TimeSpan.FromSeconds(1);
        sleep.CooldownEnd = _timing.CurTime + TimeSpan.FromSeconds(60);

        EnsureComp<HereticEtherFlaskSleepingComponent>(user);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_heretic_gain.ogg"), user);
        _popup.PopupEntity(Loc.GetString("heretic-ether-flask-used"), user, user, PopupType.Large);

        QueueDel(uid);
    }

    private void OnWakeUp(EntityUid uid, HereticEtherFlaskSleepingComponent comp, SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            return;

        RemComp<HereticEtherFlaskSleepingComponent>(uid);

        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        if (bloodstream.MetabolitesSolution != null)
            _solution.RemoveAllSolution(bloodstream.MetabolitesSolution.Value);

        if (bloodstream.TemporarySolution != null)
            _solution.RemoveAllSolution(bloodstream.TemporarySolution.Value);
    }
}
