using Content.Server.Popups;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic.Items;

public sealed class HereticLivingHeartSystem : EntitySystem
{
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;

    private static readonly SoundPathSpecifier HeartbeatSound =
        new("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticLivingHeartComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, HereticLivingHeartComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null)
            return;

        // Interaction with a rune: attempt sacrifice of tracked named target
        if (HasComp<HereticRuneComponent>(args.Target.Value))
        {
            TrySacrificeAtRune(uid, comp, args.Target.Value, args.User);
            args.Handled = true;
            return;
        }

        if (!HasComp<MobStateComponent>(args.Target.Value))
            return;
        if (args.Target.Value == args.User)
            return;

        comp.Target = args.Target.Value;
        comp.NextHeartbeat = TimeSpan.Zero;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("heretic-living-heart-target-set"), uid, args.User, PopupType.Medium);
    }

    private void TrySacrificeAtRune(EntityUid heartUid, HereticLivingHeartComponent comp, EntityUid runeUid, EntityUid user)
    {
        if (!TryComp<HereticComponent>(user, out var heretic))
            return;

        if (comp.Target == null || !Exists(comp.Target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-living-heart-no-target"), heartUid, user, PopupType.SmallCaution);
            return;
        }

        if (!_heretic.IsNamedTarget(user, comp.Target.Value))
        {
            _popup.PopupEntity(Loc.GetString("heretic-living-heart-not-named"), heartUid, user, PopupType.SmallCaution);
            return;
        }

        var runePos = _xform.GetWorldPosition(runeUid);
        var targetPos = _xform.GetWorldPosition(comp.Target.Value);
        if ((targetPos - runePos).Length() > 2f)
        {
            _popup.PopupEntity(Loc.GetString("heretic-living-heart-target-far"), heartUid, user, PopupType.SmallCaution);
            return;
        }

        var runeCoords = Transform(runeUid).Coordinates;

        var targetName = MetaData(comp.Target.Value).EntityName;
        _heretic.AddKnowledgePoints(user, heretic, 4);
        _heretic.AddSacrifice(user, 2);

        Spawn("HereticEffectRuneActivate", runeCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_castsummon.ogg"), user);

        _popup.PopupEntity(
            Loc.GetString("heretic-living-heart-sacrifice-success", ("name", targetName)),
            user, user, PopupType.Large);

        QueueDel(comp.Target);
        comp.Target = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticLivingHeartComponent>();
        while (query.MoveNext(out var uid, out var heart))
        {
            if (heart.Target == null || !Exists(heart.Target))
                continue;

            var myPos = _xform.GetWorldPosition(uid);
            var tgtPos = _xform.GetWorldPosition(heart.Target.Value);
            var dist = (tgtPos - myPos).Length();

            var t = Math.Clamp(dist / heart.TrackRange, 0f, 1f);
            var interval = heart.MinInterval + (heart.MaxInterval - heart.MinInterval) * t;

            if (_timing.CurTime < heart.NextHeartbeat)
                continue;

            heart.NextHeartbeat = _timing.CurTime + TimeSpan.FromSeconds(interval);
            _audio.PlayPvs(HeartbeatSound, uid);
        }
    }
}
