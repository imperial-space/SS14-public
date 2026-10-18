using System.Linq;
using Content.Server.Defusable.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Imperial.Disease;
using Content.Server.Popups;
using Content.Server.Administration;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Uplink.Explosives;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.Explosives;

/// <summary>
/// Взрывчатка и импланты аплинка TG: пицца-бомба, детонатор, землеразлом, граната туберкулёза,
/// импланты S3, ревивер, перезагрузчик ЦНС, тактическое отрицание, рентгеновские глаза.
/// </summary>
public sealed class UplinkExplosivesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    private static readonly SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");
    private static readonly SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/click.ogg");
    private static readonly SoundSpecifier BoxSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/box_deploy.ogg");
    private static readonly SoundSpecifier DefibSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");
    private static readonly SoundSpecifier CrackSound = new SoundPathSpecifier("/Audio/Effects/explosion1.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkPizzaBombComponent, UseInHandEvent>(OnPizzaUse);
        SubscribeLocalEvent<UplinkPizzaBombComponent, ExaminedEvent>(OnPizzaExamined);

        SubscribeLocalEvent<UplinkSyndicateDetonatorComponent, UseInHandEvent>(OnDetonatorUse);
        SubscribeLocalEvent<UplinkDetomatixComponent, UseInHandEvent>(OnDetomatixUse);
        SubscribeLocalEvent<UplinkDetomatixComponent, ExaminedEvent>(OnDetomatixExamined);

        SubscribeLocalEvent<UplinkEarthcrackerComponent, UseInHandEvent>(OnEarthcrackerUse);
        SubscribeLocalEvent<UplinkEarthcrackerComponent, InteractHandEvent>(OnEarthcrackerInteract);
        SubscribeLocalEvent<UplinkWeakpointComponent, DamageChangedEvent>(OnWeakpointDamaged);

        SubscribeLocalEvent<UplinkDiseaseGrenadeComponent, TriggerEvent>(OnDiseaseGrenade);

        SubscribeLocalEvent<UplinkDeployBoxActionEvent>(OnDeployBox);
        SubscribeLocalEvent<UplinkDeniabilityImplantComponent, ImplantImplantedEvent>(OnDeniabilityImplanted);
        SubscribeLocalEvent<UplinkDeniabilityImplantComponent, ImplantRemovedEvent>(OnDeniabilityRemoved);

        SubscribeLocalEvent<UplinkXrayEyesComponent, ComponentStartup>(OnXrayStartup);
        SubscribeLocalEvent<UplinkXrayEyesComponent, ComponentShutdown>(OnXrayShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var pizzas = EntityQueryEnumerator<UplinkPizzaBombComponent>();
        while (pizzas.MoveNext(out var uid, out var pizza))
        {
            if (pizza.DetonateAt is not { } at)
                continue;

            if (now < at)
                continue;

            pizza.DetonateAt = null;
            // bombcore/miniature: heavy 1, medium 2, light 4, flame 2.
            _explosion.QueueExplosion(_transform.GetMapCoordinates(uid), "Default", 80f, 4f, 12f, uid);
            QueueDel(uid);
        }

        var crackers = EntityQueryEnumerator<UplinkEarthcrackerComponent>();
        while (crackers.MoveNext(out var uid, out var cracker))
        {
            if (cracker.ActivateAt is not { } at || now < at)
                continue;

            cracker.ActivateAt = null;
            StrikeTheEarth((uid, cracker));
        }

        UpdateImplants(now);
    }

    #region Пицца-бомба

    private void OnPizzaExamined(Entity<UplinkPizzaBombComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Open)
            args.PushMarkup(Loc.GetString(ent.Comp.Armed ? "uplink-pizza-armed" : "uplink-pizza-unarmed"));
    }

    /// <summary>
    /// attack_self: первое открытие — выставить таймер и взвести; закрыть; следующее открытие запускает отсчёт.
    /// </summary>
    private void OnPizzaUse(Entity<UplinkPizzaBombComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Open = !ent.Comp.Open;
        _appearance.SetData(ent, UplinkPizzaBombVisuals.State, ent.Comp.Open ? (ent.Comp.DetonateAt != null ? "active" : "open") : "closed");

        if (ent.Comp.Open && ent.Comp.Armed && ent.Comp.DetonateAt == null)
        {
            _popup.PopupEntity("*бип*", ent, PopupType.MediumCaution);
            _audio.PlayPvs(BeepSound, ent);
            ent.Comp.DetonateAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Timer);
            _appearance.SetData(ent, UplinkPizzaBombVisuals.State, "active");
            return;
        }

        if (!ent.Comp.Open || ent.Comp.Armed || !_player.TryGetSessionByEntity(args.User, out var session))
            return;

        var user = args.User;
        _dialog.OpenDialog<int>(session, Loc.GetString("uplink-pizza-title"), Loc.GetString("uplink-pizza-prompt"), timer =>
        {
            ent.Comp.Timer = Math.Clamp(timer, 1, 20);
            ent.Comp.Armed = true;
            _popup.PopupEntity(Loc.GetString("uplink-pizza-set", ("timer", ent.Comp.Timer)), ent, user);
        });
    }

    #endregion

    #region Детонатор Синдиката

    /// <summary>syndicatedetonator: все активные бомбы Синдиката взрываются через BUTTON_DELAY.</summary>
    private void OnDetonatorUse(Entity<UplinkSyndicateDetonatorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_timing.CurTime < ent.Comp.ReadyAt)
            return;

        ent.Comp.ReadyAt = _timing.CurTime + TimeSpan.FromSeconds(1);
        _audio.PlayPvs(ClickSound, ent);

        int found = 0, triggered = 0;
        var bombs = EntityQueryEnumerator<DefusableComponent, TimerTriggerComponent>();
        while (bombs.MoveNext(out var uid, out var defusable, out var timer))
        {
            found++;
            if (!defusable.Activated)
                continue;

            triggered++;
            _trigger.StopTimerTrigger((uid, timer));
            _trigger.SetDelay((uid, timer), TimeSpan.FromSeconds(5));
            _trigger.ActivateTimerTrigger((uid, timer), args.User);
        }

        _popup.PopupEntity(Loc.GetString("uplink-detonator-result", ("found", found), ("triggered", triggered)), ent, args.User);
    }

    #endregion

    #region Детоматикс

    private void OnDetomatixExamined(Entity<UplinkDetomatixComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-detomatix-charges", ("charges", ent.Comp.Charges)));
    }

    /// <summary>virus/detomatix: КПК жертвы взрывается, оглушая и надолго лишая слуха.</summary>
    private void OnDetomatixUse(Entity<UplinkDetomatixComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_player.TryGetSessionByEntity(args.User, out var session))
            return;

        args.Handled = true;
        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("uplink-detomatix-empty"), ent, args.User);
            return;
        }

        var user = args.User;
        _dialog.OpenDialog<string>(session, Loc.GetString("uplink-detomatix-title"), Loc.GetString("uplink-detomatix-prompt"), name =>
        {
            var target = name.Trim();
            var pdas = EntityQueryEnumerator<Content.Shared.PDA.PdaComponent>();
            while (pdas.MoveNext(out var pda, out var comp))
            {
                if (!string.Equals(comp.OwnerName, target, StringComparison.OrdinalIgnoreCase))
                    continue;

                ent.Comp.Charges--;
                var holder = Transform(pda).ParentUid;
                _explosion.QueueExplosion(_transform.GetMapCoordinates(pda), "Default", 4f, 2f, 2f, user, canCreateVacuum: false);
                if (HasComp<MobStateComponent>(holder))
                {
                    _stun.TryKnockdown(holder, TimeSpan.FromSeconds(4));
                    _status.TryAddStatusEffectDuration(holder, "StatusEffectStutter", TimeSpan.FromSeconds(30));
                }

                QueueDel(pda);
                _popup.PopupEntity(Loc.GetString("uplink-detomatix-success"), ent, user);
                return;
            }

            _popup.PopupEntity(Loc.GetString("uplink-detomatix-notfound"), ent, user);
        });
    }

    #endregion

    #region Землеразлом

    private void OnEarthcrackerUse(Entity<UplinkEarthcrackerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Status != UplinkEarthcrackerStatus.Ready)
            return;

        args.Handled = true;
        var xform = Transform(ent);
        if (xform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("uplink-earthcracker-no-floor"), ent, args.User);
            return;
        }

        _transform.DropNextTo(ent.Owner, args.User);
        _transform.AnchorEntity(ent, Transform(ent));
        ent.Comp.Status = UplinkEarthcrackerStatus.Active;
        _popup.PopupEntity(Loc.GetString("uplink-earthcracker-armed"), ent, args.User);
    }

    private void OnEarthcrackerInteract(Entity<UplinkEarthcrackerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Status != UplinkEarthcrackerStatus.Active || ent.Comp.ActivateAt != null)
            return;

        args.Handled = true;
        ent.Comp.ActivateAt = _timing.CurTime + TimeSpan.FromSeconds(1.2);
        _popup.PopupEntity(Loc.GetString("uplink-earthcracker-activate"), ent, args.User);
    }

    /// <summary>strike_the_earth: в полу остаётся слабое место.</summary>
    private void StrikeTheEarth(Entity<UplinkEarthcrackerComponent> ent)
    {
        _audio.PlayPvs(CrackSound, ent);
        Spawn("ImperialUplinkWeakpoint", Transform(ent).Coordinates);
        ent.Comp.Status = UplinkEarthcrackerStatus.Spent;
        _transform.Unanchor(ent);
    }

    /// <summary>weakpoint/ex_act: взрыв раскалывает пол цепочкой трещин и порождает новые слабые места.</summary>
    private void OnWeakpointDamaged(Entity<UplinkWeakpointComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || TerminatingOrDeleted(ent))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var start = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var chain = new List<Vector2i> { start };
        var pos = start;
        var dir = _random.Pick(new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right });
        for (var i = 0; i < ent.Comp.CrackLength; i++)
        {
            if (_random.Prob(0.5f))
                dir = _random.Pick(new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right });

            pos += dir;
            chain.Add(pos);
        }

        foreach (var tilePos in chain.ToList())
        {
            foreach (var offset in new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right })
            {
                if (!chain.Contains(tilePos + offset))
                    chain.Add(tilePos + offset);
            }
        }

        foreach (var tilePos in chain)
        {
            var tileRef = _map.GetTileRef(gridUid, grid, tilePos);
            if (tileRef.Tile.IsEmpty)
                continue;

            _tile.DeconstructTile(tileRef);
        }

        if (ent.Comp.SpawnsChildren)
        {
            for (var i = 0; i < ent.Comp.NewWeakpoints; i++)
            {
                var spot = _random.Pick(chain);
                var child = Spawn("ImperialUplinkWeakpoint", _map.GridTileToLocal(gridUid, grid, spot));
                Comp<UplinkWeakpointComponent>(child).SpawnsChildren = false;
            }
        }

        QueueDel(ent);
    }

    #endregion

    #region Граната туберкулёза

    private void OnDiseaseGrenade(Entity<UplinkDiseaseGrenadeComponent> ent, ref TriggerEvent args)
    {
        if (!_proto.TryIndex<Content.Shared.Imperial.Disease.DiseasePrototype>(ent.Comp.Disease, out var proto))
            return;

        var disease = _disease.CreateFromPrototype(proto);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(ent), ent.Comp.Range))
        {
            _disease.TryInfect(mob, disease);
        }
    }

    #endregion

    #region Импланты

    /// <summary>agent_box: коробка-хамелеон вокруг носителя; внутри — открыть её.</summary>
    private void OnDeployBox(UplinkDeployBoxActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        if (_container.TryGetContainingContainer(user, out var container) && HasComp<EntityStorageComponent>(container.Owner))
        {
            _storage.OpenStorage(container.Owner);
            _audio.PlayPvs(BoxSound, user);
            args.Handled = true;
            return;
        }

        if (!TryComp<UplinkStealthImplantComponent>(args.Action.Comp.Container, out var implant))
            implant = null;

        var box = Spawn(implant?.Box ?? "StealthBox", Transform(user).Coordinates);
        _storage.Insert(user, box);
        _storage.CloseStorage(box);
        _audio.PlayPvs(BoxSound, user);
        args.Handled = true;
    }

    private void OnDeniabilityImplanted(Entity<UplinkDeniabilityImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        // TRAIT_NOSOFTCRIT/NOHARDCRIT: крит сдвигается почти до смерти.
        if (!_thresholds.TryGetThresholdForState(args.Implanted, MobState.Critical, out var crit)
            || !_thresholds.TryGetThresholdForState(args.Implanted, MobState.Dead, out var dead))
            return;

        ent.Comp.OriginalCrit = crit;
        _thresholds.SetMobStateThreshold(args.Implanted, dead.Value - 1, MobState.Critical);
    }

    private void OnDeniabilityRemoved(Entity<UplinkDeniabilityImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (ent.Comp.OriginalCrit is { } crit)
            _thresholds.SetMobStateThreshold(args.Implanted, crit, MobState.Critical);
    }

    private void OnXrayStartup(Entity<UplinkXrayEyesComponent> ent, ref ComponentStartup args)
    {
        _eye.SetDrawFov(ent, false);
    }

    private void OnXrayShutdown(Entity<UplinkXrayEyesComponent> ent, ref ComponentShutdown args)
    {
        _eye.SetDrawFov(ent, true);
    }

    private void UpdateImplants(TimeSpan now)
    {
        var deniability = EntityQueryEnumerator<UplinkDeniabilityImplantComponent, SubdermalImplantComponent>();
        while (deniability.MoveNext(out var uid, out var implant, out var subdermal))
        {
            if (subdermal.ImplantedEntity is not { } owner || implant.OriginalCrit is not { } crit)
                continue;

            if (implant.DetonateAt is { } at)
            {
                if (now >= at)
                {
                    implant.DetonateAt = null;
                    _explosion.QueueExplosion(_transform.GetMapCoordinates(owner), "Default", 120f, 5f, 15f, owner);
                    _gibbing.Gib(owner);
                }

                continue;
            }

            if (TryComp<DamageableComponent>(owner, out var damageable) && _damageable.GetTotalDamage((owner, damageable)) >= crit)
            {
                implant.DetonateAt = now + TimeSpan.FromSeconds(10);
                _popup.PopupEntity(Loc.GetString("uplink-deniability-armed"), owner, PopupType.LargeCaution);
                _audio.PlayPvs(BeepSound, owner);
            }
        }

        var revivers = EntityQueryEnumerator<UplinkReviverImplantComponent, SubdermalImplantComponent>();
        while (revivers.MoveNext(out _, out var reviver, out var subdermal))
        {
            if (subdermal.ImplantedEntity is not { } owner || now < reviver.NextHeal)
                continue;

            reviver.NextHeal = now + TimeSpan.FromSeconds(3);
            TryHeal(owner, reviver, now);
        }

        var rebooters = EntityQueryEnumerator<UplinkCnsRebooterComponent, SubdermalImplantComponent>();
        while (rebooters.MoveNext(out _, out var rebooter, out var subdermal))
        {
            if (subdermal.ImplantedEntity is not { } owner)
                continue;

            var stunned = HasComp<StunnedComponent>(owner) || HasComp<KnockedDownComponent>(owner);
            if (now < rebooter.ImmuneUntil && stunned)
            {
                ClearStuns(owner);
                continue;
            }

            if (rebooter.ClearAt is { } clearAt)
            {
                if (now < clearAt)
                    continue;

                rebooter.ClearAt = null;
                ClearStuns(owner);
                rebooter.ImmuneUntil = now + TimeSpan.FromSeconds(6);
                rebooter.ReadyAt = now + TimeSpan.FromSeconds(60);
                _popup.PopupEntity(Loc.GetString("uplink-cns-reboot"), owner, owner, PopupType.Medium);
                continue;
            }

            if (stunned && now >= rebooter.ReadyAt)
                rebooter.ClearAt = now + TimeSpan.FromSeconds(1);
        }
    }

    private void ClearStuns(EntityUid owner)
    {
        _stun.TryUnstun(owner);
        _stun.SetKnockdownTime(owner, TimeSpan.Zero);
        _status.TryRemoveStatusEffect(owner, SharedStunSystem.StunId);
    }

    /// <summary>reviver/try_heal + heal + revive_dead.</summary>
    private void TryHeal(EntityUid owner, UplinkReviverImplantComponent reviver, TimeSpan now)
    {
        if (!TryComp<MobStateComponent>(owner, out var mobState))
            return;

        var conscious = mobState.CurrentState == MobState.Alive;
        if (reviver.Reviving)
        {
            if (conscious)
            {
                reviver.Reviving = false;
                reviver.ReadyAt = now + TimeSpan.FromSeconds(reviver.ReviveCost / 10f);
                _popup.PopupEntity(Loc.GetString("uplink-reviver-shutdown", ("time", (int) (reviver.ReviveCost / 10f))), owner, owner);
                return;
            }

            Heal(owner, reviver);
            if (mobState.CurrentState == MobState.Dead && now >= reviver.DefibAt)
                ReviveDead(owner, reviver);
            return;
        }

        if (now < reviver.ReadyAt || conscious)
            return;

        reviver.ReviveCost = 0;
        reviver.Reviving = true;
        reviver.DefibAt = now + TimeSpan.FromSeconds(8);
        _popup.PopupEntity(Loc.GetString("uplink-reviver-start"), owner, owner);
    }

    private void Heal(EntityUid owner, UplinkReviverImplantComponent reviver)
    {
        if (!TryComp<DamageableComponent>(owner, out var damageable))
            return;

        var heal = new DamageSpecifier();
        var dict = _damageable.GetAllDamage((owner, damageable)).DamageDict;
        if (dict.GetValueOrDefault("Asphyxiation") > 0)
        {
            heal.DamageDict["Asphyxiation"] = -5;
            reviver.ReviveCost += 5;
        }

        foreach (var brute in new[] { "Blunt", "Slash", "Piercing" })
        {
            if (dict.GetValueOrDefault(brute) > 0)
            {
                heal.DamageDict[brute] = -2f / 3f;
                reviver.ReviveCost += 40f / 3f;
            }
        }

        foreach (var burn in new[] { "Heat", "Cold", "Shock" })
        {
            if (dict.GetValueOrDefault(burn) > 0)
            {
                heal.DamageDict[burn] = -2f / 3f;
                reviver.ReviveCost += 40f / 3f;
            }
        }

        if (dict.GetValueOrDefault("Poison") > 0)
        {
            heal.DamageDict["Poison"] = -1;
            reviver.ReviveCost += 40;
        }

        if (heal.DamageDict.Count > 0)
            _damageable.TryChangeDamage(owner, heal, true, false);
    }

    private void ReviveDead(EntityUid owner, UplinkReviverImplantComponent reviver)
    {
        if (!_thresholds.TryGetThresholdForState(owner, MobState.Dead, out var dead)
            || !TryComp<DamageableComponent>(owner, out var damageable)
            || _damageable.GetTotalDamage((owner, damageable)) >= dead)
            return;

        reviver.ReviveCost += 6000; // revive_cost += 10 MINUTES
        reviver.DefibAt = _timing.CurTime + TimeSpan.FromSeconds(16);
        _audio.PlayPvs(DefibSound, owner);
        _popup.PopupEntity(Loc.GetString("uplink-reviver-revive", ("target", owner)), owner, PopupType.MediumCaution);
        _mobState.ChangeMobState(owner, MobState.Critical);
        if (_thresholds.TryGetThresholdForState(owner, MobState.Critical, out var crit) && _damageable.GetTotalDamage((owner, damageable)) < crit)
            _mobState.ChangeMobState(owner, MobState.Alive);
    }

    #endregion
}
