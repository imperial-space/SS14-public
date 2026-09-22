using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.KeyRing;
using Content.Shared.Teleportation.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.KeyRing;

public sealed class HereticLockPortalSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundPathSpecifier SoundDeparture = new("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundPathSpecifier SoundArrival = new("/Audio/Effects/teleport_arrival.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticLockPortalComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<HereticLockPortalComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnCollide(EntityUid uid, HereticLockPortalComponent comp, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != "portalFixture") return;

        var subject = args.OtherEntity;

        if (HasComp<PortalTimeoutComponent>(subject)) return;
        if (Transform(subject).Anchored) return;

        var isHeretic = HasComp<HereticComponent>(subject);
        // XOR: без инверсии — еретик → к партнёру; с инверсией — наоборот
        var toPartner = comp.Inverted ? !isHeretic : isHeretic;

        if (toPartner && comp.Partner.HasValue && Exists(comp.Partner.Value))
        {
            TeleportToPartner(subject, uid, comp.Partner.Value);
        }
        else
        {
            TeleportRandomly(subject, uid);
            // Урон только тем, кто не должен был сюда попасть
            if (!toPartner)
            {
                _damage.TryChangeDamage(subject, new DamageSpecifier
                {
                    DamageDict = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>
                    {
                        ["Blunt"] = 20
                    }
                }, ignoreResistances: false);
            }
        }
    }

    private void OnEndCollide(EntityUid uid, HereticLockPortalComponent comp, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != "portalFixture") return;

        var subject = args.OtherEntity;
        if (TryComp<PortalTimeoutComponent>(subject, out var timeout) && timeout.EnteredPortal != uid)
            RemCompDeferred<PortalTimeoutComponent>(subject);
    }

    private void TeleportToPartner(EntityUid subject, EntityUid fromPortal, EntityUid toPortal)
    {
        var timeout = EnsureComp<PortalTimeoutComponent>(subject);
        timeout.EnteredPortal = fromPortal;
        Dirty(subject, timeout);

        _transform.SetCoordinates(subject, Transform(toPortal).Coordinates);

        _audio.PlayPvs(SoundDeparture, fromPortal);
        _audio.PlayPvs(SoundArrival, toPortal);
    }

    private void TeleportRandomly(EntityUid subject, EntityUid fromPortal)
    {
        const float maxRadius = 7f;

        var origin = _transform.GetMapCoordinates(fromPortal);
        var offset = _random.NextVector2(maxRadius);
        var dest = new MapCoordinates(origin.Position + offset, origin.MapId);

        _transform.SetMapCoordinates(subject, dest);

        _audio.PlayPvs(SoundDeparture, fromPortal);
        _audio.PlayPvs(SoundArrival, subject);
    }
}
