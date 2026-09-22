using Content.Server.Doors.Systems;
using Content.Server.Popups;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticEldritchCoinSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DoorSystem _door = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticEldritchCoinComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<HereticEldritchCoinComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnUseInHand(EntityUid uid, HereticEldritchCoinComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var heads = _random.Prob(0.5f);

        if (!HasComp<HereticComponent>(user))
        {
            DamageNonBeliever(user, heads);
            return;
        }

        _appearance.SetData(uid, HereticCoinVisuals.Flipping, true);
        _audio.PlayPvs(new SoundCollectionSpecifier("Dice"), uid);

        var coinUid = uid;
        var userUid = user;
        var coords = Transform(user).Coordinates;
        var flipRange = comp.FlipRange;

        Timer.Spawn(TimeSpan.FromSeconds(1), () =>
        {
            if (Deleted(coinUid))
                return;

            _appearance.SetData(coinUid, HereticCoinVisuals.Flipping, false);

            if (heads)
            {
                foreach (var door in _lookup.GetEntitiesInRange<DoorComponent>(coords, flipRange))
                    _door.TryOpen(door.Owner);
                _popup.PopupEntity(Loc.GetString("heretic-eldritch-coin-heads"), userUid, userUid, PopupType.Medium);
            }
            else
            {
                foreach (var boltEnt in _lookup.GetEntitiesInRange<DoorBoltComponent>(coords, flipRange))
                    _door.TrySetBoltDown(boltEnt, !boltEnt.Comp.BoltsDown);
                _popup.PopupEntity(Loc.GetString("heretic-eldritch-coin-tails"), userUid, userUid, PopupType.Medium);
            }
        });
    }

    private void OnAfterInteract(EntityUid uid, HereticEldritchCoinComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var target = args.Target.Value;
        if (!HasComp<DoorComponent>(target))
            return;

        args.Handled = true;
        var user = args.User;

        if (!HasComp<HereticComponent>(user))
        {
            DamageNonBeliever(user, _random.Prob(0.5f));
            return;
        }

        if (TryComp<DoorBoltComponent>(target, out var boltComp) && boltComp.BoltsDown)
        {
            _popup.PopupEntity(Loc.GetString("heretic-eldritch-coin-bolted"), user, user, PopupType.SmallCaution);
            return;
        }

        _door.TryOpen(target);
        _popup.PopupEntity(Loc.GetString("heretic-eldritch-coin-used"), user, user, PopupType.Small);
        QueueDel(uid);
    }

    private void DamageNonBeliever(EntityUid user, bool heads)
    {
        var dmg = new DamageSpecifier();
        if (heads)
        {
            dmg.DamageDict["Heat"] = 20;
            _popup.PopupEntity(Loc.GetString("heretic-eldritch-coin-nonbeliever-burn"), user, user, PopupType.MediumCaution);
        }
        else
        {
            dmg.DamageDict["Blunt"] = 20;
            _popup.PopupEntity(Loc.GetString("heretic-eldritch-coin-nonbeliever-blunt"), user, user, PopupType.MediumCaution);
        }
        _damageable.TryChangeDamage(user, dmg, ignoreResistances: true);
    }
}
