using Content.Server.Popups;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmicBeaconSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCosmicBeaconComponent, UseInHandEvent>(OnBeaconUseInHand);
        SubscribeLocalEvent<HereticCosmicBeaconComponent, ActivateInWorldEvent>(OnBeaconActivate);
    }

    private void OnBeaconActivate(EntityUid uid, HereticCosmicBeaconComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled) return;
        TryTeleport(uid, comp, args.User);
        args.Handled = true;
    }

    private void OnBeaconUseInHand(EntityUid uid, HereticCosmicBeaconComponent comp, UseInHandEvent args)
    {
        if (args.Handled) return;
        TryTeleport(uid, comp, args.User);
        args.Handled = true;
    }

    private void TryTeleport(EntityUid beaconUid, HereticCosmicBeaconComponent beacon, EntityUid user)
    {
        if (beacon.Caster != user)
        {
            _popup.PopupEntity(Loc.GetString("heretic-cosmic-beacon-not-yours"), user, user);
            return;
        }

        EntityUid? partner = null;
        var query = EntityQueryEnumerator<HereticCosmicBeaconComponent>();
        while (query.MoveNext(out var otherUid, out var otherComp))
        {
            if (otherUid == beaconUid) continue;
            if (otherComp.Caster == user)
            {
                partner = otherUid;
                break;
            }
        }

        if (partner == null)
        {
            _popup.PopupEntity(Loc.GetString("heretic-cosmic-beacon-no-partner"), user, user);
            return;
        }

        _xform.SetCoordinates(user, Transform(partner.Value).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/blink.ogg"), user);
        _popup.PopupEntity(Loc.GetString("heretic-cosmic-beacon-teleport"), user, user, PopupType.Medium);
    }

    public void OnBeaconCreated(EntityUid beaconUid, HereticCosmicBeaconComponent beacon, EntityUid caster)
    {
        beacon.Caster = caster;

        // Если у кастера уже есть 2 маяка — удалить старейший лишний
        var beacons = new List<EntityUid>();
        var query = EntityQueryEnumerator<HereticCosmicBeaconComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Caster == caster)
                beacons.Add(uid);
        }

        // beaconUid уже в списке (только что создан). Если >= 3 — удалить первый кроме нового
        if (beacons.Count >= 3)
        {
            foreach (var old in beacons)
            {
                if (old != beaconUid)
                {
                    QueueDel(old);
                    break;
                }
            }
        }
    }
}
