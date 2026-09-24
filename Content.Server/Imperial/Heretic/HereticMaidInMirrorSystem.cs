using Content.Server.Beam;
using Content.Server.Beam.Components;
using Content.Server.Mind;
using Content.Shared.Beam.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.MagicMirror;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMaidInMirrorSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    private static readonly EntProtoId MirrorBallProto = "MobHereticMaidMirrorBall";
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly SoundPathSpecifier PhaseEnterSound =
        new("/Audio/Imperial/heretic/sound_effects_magic_ethereal_enter.ogg");
    private static readonly SoundPathSpecifier PhaseExitSound =
        new("/Audio/Imperial/heretic/sound_effects_magic_ethereal_exit.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMaidInMirrorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<HereticMaidInMirrorComponent, HereticMaidInMirrorPhaseActionEvent>(OnPhaseAction);
        SubscribeLocalEvent<HereticMaidInMirrorComponent, HereticMaidMirrorEnterDoAfterEvent>(OnMirrorEnterDoAfter);
        SubscribeLocalEvent<HereticMaidInMirrorComponent, MobStateChangedEvent>(OnMaidDied);
        SubscribeLocalEvent<HereticMaidMirrorBallComponent, HereticMaidMirrorExitActionEvent>(OnBallExit);
        SubscribeLocalEvent<HereticMaidMirrorBallComponent, EntityTerminatingEvent>(OnBallTerminating);
        SubscribeLocalEvent<HereticMaidMirrorBallComponent, GetVisMaskEvent>(OnBallGetVisMask);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<HereticMaidMirrorBallComponent>();
        while (query.MoveNext(out _, out var ball))
        {
            ball.HealTimer += frameTime;
            if (ball.HealTimer < ball.HealInterval)
                continue;
            ball.HealTimer = 0f;
            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"] = FixedPoint2.New(-ball.HealAmount);
            _damage.TryChangeDamage(ball.MaidUid, heal, ignoreResistances: true);
        }
    }

    private void OnPhaseAction(EntityUid uid, HereticMaidInMirrorComponent maid, HereticMaidInMirrorPhaseActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (maid.IsInMirrorWorld || maid.BeamControllerUid.HasValue)
            return;

        if (!TryFindMirrorNearby(uid, out var mirrorUid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-maid-mirror-no-mirror"), uid, uid, PopupType.SmallCaution);
            return;
        }

        var controller = Spawn("VirtualBeamEntityController", Transform(uid).Coordinates);
        _beam.TryCreateBeam(uid, mirrorUid, "HereticMaidMirrorBeam", null, "unshaded", controller);
        maid.BeamControllerUid = controller;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            uid,
            TimeSpan.FromSeconds(3f),
            new HereticMaidMirrorEnterDoAfterEvent(),
            uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnMirrorEnterDoAfter(EntityUid uid, HereticMaidInMirrorComponent maid, HereticMaidMirrorEnterDoAfterEvent args)
    {
        CleanupBeam(maid);
        if (args.Cancelled)
            return;
        EnterMirrorWorld(uid, maid);
    }

    private void CleanupBeam(HereticMaidInMirrorComponent maid)
    {
        if (!maid.BeamControllerUid.HasValue)
            return;
        var controllerUid = maid.BeamControllerUid.Value;
        maid.BeamControllerUid = null;

        var beamQuery = EntityQueryEnumerator<BeamComponent>();
        while (beamQuery.MoveNext(out var beamUid, out var beamComp))
        {
            if (beamComp.VirtualBeamController == controllerUid)
                QueueDel(beamUid);
        }
        QueueDel(controllerUid);
    }

    private bool TryFindMirrorNearby(EntityUid uid, out EntityUid mirrorUid)
    {
        mirrorUid = default;
        var coords = Transform(uid).Coordinates;

        foreach (var ent in _lookup.GetEntitiesInRange<MagicMirrorComponent>(coords, 2f))
        {
            mirrorUid = ent.Owner;
            return true;
        }

        foreach (var ent in _lookup.GetEntitiesInRange<TagComponent>(coords, 2f))
        {
            if (_tag.HasTag(ent.Owner, WindowTag))
            {
                mirrorUid = ent.Owner;
                return true;
            }
        }

        return false;
    }

    private void OnBallExit(EntityUid uid, HereticMaidMirrorBallComponent ball, HereticMaidMirrorExitActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (!HasWindowNearby(uid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-maid-mirror-no-window"), uid, uid, PopupType.SmallCaution);
            return;
        }

        ExitMirrorWorld(uid, ball);
    }

    private void OnBallTerminating(EntityUid uid, HereticMaidMirrorBallComponent ball, ref EntityTerminatingEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;

        _xform.SetCoordinates(ball.MaidUid, Transform(uid).Coordinates);
        _mind.TransferTo(mindId, ball.MaidUid, ghostCheckOverride: true);

        if (TryComp<HereticMaidInMirrorComponent>(ball.MaidUid, out var maid))
        {
            maid.IsInMirrorWorld = false;
            maid.MirrorBallUid = null;
        }
    }

    private void OnMaidDied(EntityUid uid, HereticMaidInMirrorComponent maid, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !maid.IsInMirrorWorld || !maid.MirrorBallUid.HasValue)
            return;

        var ballUid = maid.MirrorBallUid.Value;
        if (_mind.TryGetMind(ballUid, out var mindId, out _))
            _mind.TransferTo(mindId, uid, ghostCheckOverride: true);

        maid.IsInMirrorWorld = false;
        maid.MirrorBallUid = null;
        QueueDel(ballUid);
    }

    private bool HasWindowNearby(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<TagComponent>(coords, 1.5f))
        {
            if (_tag.HasTag(ent.Owner, WindowTag))
                return true;
        }
        return false;
    }

    private void EnterMirrorWorld(EntityUid uid, HereticMaidInMirrorComponent maid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;

        var coords = Transform(uid).Coordinates;
        var ballUid = Spawn(MirrorBallProto, coords);

        if (!TryComp<HereticMaidMirrorBallComponent>(ballUid, out var ball))
        {
            QueueDel(ballUid);
            return;
        }

        ball.MaidUid = uid;
        maid.MirrorBallUid = ballUid;
        maid.IsInMirrorWorld = true;

        _xform.DetachParentToNull(uid, Transform(uid));

        var ballVisibility = EnsureComp<VisibilityComponent>(ballUid);
        _visibility.AddLayer((ballUid, ballVisibility), (int)VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((ballUid, ballVisibility), (int)VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(ballUid, visibilityComponent: ballVisibility);

        _mind.TransferTo(mindId, ballUid, ghostCheckOverride: true);
        _eye.RefreshVisibilityMask(ballUid);

        _audio.PlayPvs(PhaseEnterSound, ballUid);
        _popup.PopupEntity(Loc.GetString("heretic-maid-mirror-enter"), ballUid, ballUid, PopupType.Medium);
    }

    private void ExitMirrorWorld(EntityUid ballUid, HereticMaidMirrorBallComponent ball)
    {
        if (!_mind.TryGetMind(ballUid, out var mindId, out _))
        {
            QueueDel(ballUid);
            return;
        }

        var maidUid = ball.MaidUid;
        var ballCoords = Transform(ballUid).Coordinates;
        _xform.SetCoordinates(maidUid, ballCoords);

        _mind.TransferTo(mindId, maidUid, ghostCheckOverride: true);

        if (TryComp<HereticMaidInMirrorComponent>(maidUid, out var maid))
        {
            maid.IsInMirrorWorld = false;
            maid.MirrorBallUid = null;
        }

        _audio.PlayPvs(PhaseExitSound, maidUid);
        _popup.PopupEntity(Loc.GetString("heretic-maid-mirror-exit"), maidUid, maidUid, PopupType.Medium);

        QueueDel(ballUid);
    }

    private void OnExamined(Entity<HereticMaidInMirrorComponent> ent, ref ExaminedEvent args)
    {
        var now = _timing.CurTime;
        var examiner = args.Examiner;

        if (ent.Comp.RecentExaminers.TryGetValue(examiner, out var lastTime))
        {
            if ((now - lastTime).TotalSeconds < ent.Comp.ExamineHarmCooldown)
                return;
        }
        ent.Comp.RecentExaminers[examiner] = now;

        if (!TryComp<DamageableComponent>(ent.Owner, out var damageable))
            return;

        var totalDamage = _damage.GetTotalDamage((ent.Owner, damageable));
        var maxHp = ent.Comp.MaxHp;

        if (totalDamage.Float() >= maxHp * 0.98f)
        {
            var lethal = new DamageSpecifier();
            lethal.DamageDict["Slash"] = FixedPoint2.New(maxHp);
            _damage.TryChangeDamage(ent.Owner, lethal, ignoreResistances: true);
        }
        else
        {
            var harm = new DamageSpecifier();
            harm.DamageDict["Slash"] = FixedPoint2.New(maxHp * 0.02f);
            _damage.TryChangeDamage(ent.Owner, harm, ignoreResistances: true);
        }
    }

    private void OnBallGetVisMask(EntityUid uid, HereticMaidMirrorBallComponent comp, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Ghost;
    }
}
