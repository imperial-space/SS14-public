using System.Linq;
using System.Numerics;
using Content.Server.Forensics;
using Content.Server.Silicons.Laws;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Uplink.Stealth;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Server.Administration;
using Robust.Shared.Random;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Station;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server.Imperial.Uplink.Stealth;

public sealed partial class UplinkStealthSystem
{
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly Content.Server.Station.Systems.StationSystem _station = default!;

    private static readonly SoundSpecifier PhaseInSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/phasein.ogg");
    private static readonly SoundSpecifier TeleportSound = new SoundCollectionSpecifier("sparks");
    private static readonly SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    private void InitializeGadgets()
    {
        SubscribeLocalEvent<UplinkAiDetectorComponent, ExaminedEvent>(OnAiDetectorExamined);

        SubscribeLocalEvent<UplinkEmpFlashlightComponent, AfterInteractEvent>(OnEmpFlashlightInteract);
        SubscribeLocalEvent<UplinkEmpFlashlightComponent, ExaminedEvent>(OnEmpFlashlightExamined);

        SubscribeLocalEvent<UplinkMulliganComponent, AfterInteractEvent>(OnMulliganInteract);
        SubscribeLocalEvent<UplinkMulliganComponent, UseInHandEvent>(OnMulliganUse);
        SubscribeLocalEvent<UplinkFakeIdentityKitComponent, UseInHandEvent>(OnFakeIdentityUse);

        SubscribeLocalEvent<UplinkForensicsSpooferComponent, AfterInteractEvent>(OnSpooferInteract);
        SubscribeLocalEvent<UplinkForensicsSpooferComponent, UseInHandEvent>(OnSpooferUse);
        SubscribeLocalEvent<UplinkForensicsSpooferComponent, ExaminedEvent>(OnSpooferExamined);

        SubscribeLocalEvent<UplinkRadLaserComponent, AfterInteractEvent>(OnRadLaserInteract);
        SubscribeLocalEvent<UplinkRadLaserComponent, GetVerbsEvent<AlternativeVerb>>(OnRadLaserVerbs);
        SubscribeLocalEvent<UplinkRadLaserComponent, ExaminedEvent>(OnRadLaserExamined);

        SubscribeLocalEvent<UplinkSyndicateTeleporterComponent, UseInHandEvent>(OnTeleporterUse);
        SubscribeLocalEvent<UplinkSyndicateTeleporterComponent, ExaminedEvent>(OnTeleporterExamined);
        SubscribeLocalEvent<UplinkSyndicateTeleporterComponent, EmpPulseEvent>(OnTeleporterEmp);

        SubscribeLocalEvent<UplinkHackedLawModuleComponent, UseInHandEvent>(OnHackedModuleUse);
        SubscribeLocalEvent<UplinkHackedLawModuleComponent, ContainerGettingInsertedAttemptEvent>(OnHackedModuleInsertAttempt);
        SubscribeLocalEvent<UplinkHackedLawModuleComponent, ExaminedEvent>(OnHackedModuleExamined);

        SubscribeLocalEvent<UplinkCardboardCutoutComponent, InteractUsingEvent>(OnCutoutInteract);
        SubscribeLocalEvent<UplinkCardboardCutoutComponent, ComponentStartup>(OnCutoutStartup);

        SubscribeLocalEvent<UplinkHypnoticFlashComponent, AfterFlashedEvent>(OnHypnoticFlashed);
        SubscribeLocalEvent<UplinkHypnoticGrenadeComponent, TriggerEvent>(OnHypnoticGrenade);
        SubscribeLocalEvent<UplinkHypnoticTranceComponent, ListenEvent>(OnTranceListen);
        SubscribeLocalEvent<UplinkHypnoticTranceComponent, ListenAttemptEvent>(OnTranceListenAttempt);
    }

    private void UpdateGadgets()
    {
        ApplyPendingLaws();

        var now = _timing.CurTime;

        var detectors = EntityQueryEnumerator<UplinkAiDetectorComponent>();
        while (detectors.MoveNext(out var uid, out var detector))
        {
            if (now < detector.NextCheck)
                continue;

            detector.NextCheck = now + TimeSpan.FromSeconds(1);
            var state = GetAiProximity(uid, detector);
            if (state == detector.State)
                continue;

            detector.State = state;
            Dirty(uid, detector);
            _appearance.SetData(uid, UplinkAiDetectorVisuals.State, state);
        }

        var lights = EntityQueryEnumerator<UplinkEmpFlashlightComponent>();
        while (lights.MoveNext(out var uid, out var light))
        {
            if (light.Charges >= light.MaxCharges)
            {
                light.NextCharge = now + light.ChargeDelay;
                continue;
            }

            if (now < light.NextCharge)
                continue;

            light.Charges++;
            light.NextCharge = now + light.ChargeDelay;
        }

        var victims = EntityQueryEnumerator<UplinkRadLaserVictimComponent>();
        while (victims.MoveNext(out var uid, out var victim))
        {
            if (now < victim.TriggerAt)
                continue;

            // radiation_aftereffect: нокаут на intensity * 4/3 секунды при intensity >= 5.
            if (victim.Intensity >= 5)
                _status.TryAddStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, Content.Shared.Imperial.Balance.ImperialControlNorms.Sleep(TimeSpan.FromSeconds(victim.Intensity / 0.75f)));

            var rad = new DamageSpecifier();
            rad.DamageDict["Radiation"] = victim.Intensity * 2;
            _damageable.TryChangeDamage(uid, rad, true);
            RemCompDeferred<UplinkRadLaserVictimComponent>(uid);
        }

        var teleporters = EntityQueryEnumerator<UplinkSyndicateTeleporterComponent>();
        while (teleporters.MoveNext(out var uid, out var tele))
        {
            if (now < tele.NextRechargeRoll)
                continue;

            tele.NextRechargeRoll = now + TimeSpan.FromSeconds(1);
            // SPT_PROB(10, seconds_per_tick): 5% в секунду.
            if (tele.Charges >= tele.MaxCharges || !_random.Prob(0.05f))
                continue;

            tele.Charges++;
            _audio.PlayPvs(BeepSound, uid, AudioParams.Default.WithVolume(-10f).WithMaxDistance(2f));
        }

        var trances = EntityQueryEnumerator<UplinkHypnoticTranceComponent>();
        while (trances.MoveNext(out var uid, out var trance))
        {
            if (now >= trance.EndTime)
            {
                RemCompDeferred<UplinkHypnoticTranceComponent>(uid);
                RemCompDeferred<ActiveListenerComponent>(uid);
            }
        }

        var hypnotized = EntityQueryEnumerator<UplinkHypnotizedComponent>();
        while (hypnotized.MoveNext(out var uid, out var hypno))
        {
            if (now < hypno.NextReminder)
                continue;

            hypno.NextReminder = now + TimeSpan.FromSeconds(_random.Next(60, 120));
            _popup.PopupEntity(hypno.Phrase, uid, uid, PopupType.LargeCaution);
        }
    }

    #region Детектор ИИ

    private UplinkAiProximity GetAiProximity(EntityUid uid, UplinkAiDetectorComponent detector)
    {
        var ourPos = _transform.GetMapCoordinates(uid);
        var best = UplinkAiProximity.None;

        var cores = EntityQueryEnumerator<StationAiCoreComponent>();
        while (cores.MoveNext(out _, out var core))
        {
            if (core.RemoteEntity is not { } eye || TerminatingOrDeleted(eye))
                continue;

            var eyePos = _transform.GetMapCoordinates(eye);
            if (eyePos.MapId != ourPos.MapId)
                continue;

            var dist = (eyePos.Position - ourPos.Position).Length();
            if (dist <= detector.RangeAlert)
                return UplinkAiProximity.OnScreen;

            if (dist <= detector.RangeWarning)
                best = UplinkAiProximity.Near;
        }

        return best;
    }

    private void OnAiDetectorExamined(Entity<UplinkAiDetectorComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.State switch
        {
            UplinkAiProximity.OnScreen => "uplink-ai-detector-onscreen",
            UplinkAiProximity.Near => "uplink-ai-detector-near",
            _ => "uplink-ai-detector-none",
        }));
    }

    #endregion

    #region ЭМИ-фонарик

    private void OnEmpFlashlightExamined(Entity<UplinkEmpFlashlightComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-emp-flashlight-charges", ("charges", ent.Comp.Charges)));
    }

    private void OnEmpFlashlightInteract(Entity<UplinkEmpFlashlightComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("uplink-emp-flashlight-recharging"), ent, args.User);
            return;
        }

        ent.Comp.Charges--;
        if (ent.Comp.Charges == ent.Comp.MaxCharges - 1)
            ent.Comp.NextCharge = _timing.CurTime + ent.Comp.ChargeDelay;

        _popup.PopupEntity(Loc.GetString("uplink-emp-flashlight-blink", ("user", args.User), ("target", target)), target, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("uplink-emp-flashlight-charges", ("charges", ent.Comp.Charges)), ent, args.User);
        // emp_act(EMP_HEAVY)
        _emp.DoEmpEffects(target, 100000f, TimeSpan.FromSeconds(20), args.User);
    }

    #endregion

    #region Маллиган и поддельная личность

    private void OnMulliganUse(Entity<UplinkMulliganComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ApplyMulligan(ent, args.User);
    }

    private void OnMulliganInteract(Entity<UplinkMulliganComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        args.Handled = true;
        ApplyMulligan(ent, target);
    }

    /// <summary>/datum/reagent/mulligan: randomize_human_normie — новая внешность и имя.</summary>
    private void ApplyMulligan(Entity<UplinkMulliganComponent> ent, EntityUid target)
    {
        if (!TryComp<HumanoidProfileComponent>(target, out var humanoid))
            return;

        var profile = HumanoidCharacterProfile.RandomWithSpecies(humanoid.Species);
        _humanoidProfile.ApplyProfileTo((target, humanoid), profile);
        _metaData.SetEntityName(target, profile.Name);

        _popup.PopupEntity(Loc.GetString("uplink-mulligan-self"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("uplink-mulligan-others", ("target", target)), target, Robust.Shared.Player.Filter.PvsExcept(target), true, PopupType.MediumCaution);
        QueueDel(ent);
    }

    /// <summary>fake_identity_kit: ID пассажира на новое имя и объявление о прибытии.</summary>
    private void OnFakeIdentityUse(Entity<UplinkFakeIdentityKitComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.Handled = true;
        var name = Name(args.User);
        var card = Spawn(ent.Comp.IdCard, Transform(args.User).Coordinates);
        _idCard.TryChangeFullName(card, name);
        if (_proto.TryIndex(ent.Comp.Job, out var job))
            _idCard.TryChangeJobTitle(card, job.LocalizedName);

        if (_inventory.TryGetSlotEntity(args.User, "id", out var oldId))
        {
            _inventory.TryUnequip(args.User, "id", true, true);
            _hands.PickupOrDrop(args.User, oldId.Value);
        }

        if (!_inventory.TryEquip(args.User, card, "id", true, true))
            _hands.PickupOrDrop(args.User, card);

        if (_station.GetOwningStation(args.User) is { } station)
        {
            var jobName = job?.LocalizedName ?? string.Empty;
            _chat.DispatchStationAnnouncement(station,
                Loc.GetString("latejoin-arrival-announcement", ("character", name), ("entity", args.User), ("job", jobName)),
                Loc.GetString("latejoin-arrival-sender"),
                playDefaultSound: false);
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Paper/paper_scribble1.ogg"), args.User);
        QueueDel(ent);
    }

    #endregion

    #region Подделыватель криминалистики

    private void OnSpooferExamined(Entity<UplinkForensicsSpooferComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.ScanMode ? "uplink-spoofer-mode-scan" : "uplink-spoofer-mode-apply"));
        args.PushMarkup(Loc.GetString("uplink-spoofer-stored", ("prints", ent.Comp.Fingerprints.Count), ("fibers", ent.Comp.Fibers.Count)));
    }

    private void OnSpooferUse(Entity<UplinkForensicsSpooferComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.ScanMode = !ent.Comp.ScanMode;
        _popup.PopupEntity(Loc.GetString(ent.Comp.ScanMode ? "uplink-spoofer-mode-scan" : "uplink-spoofer-mode-apply"), ent, args.User);
    }

    private void OnSpooferInteract(Entity<UplinkForensicsSpooferComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (ent.Comp.ScanMode)
        {
            if (!TryComp<ForensicsComponent>(target, out var forensics))
            {
                _popup.PopupEntity(Loc.GetString("uplink-spoofer-nothing"), ent, args.User);
                return;
            }

            ent.Comp.Fingerprints.UnionWith(forensics.Fingerprints);
            ent.Comp.Fibers.UnionWith(forensics.Fibers);
            _popup.PopupEntity(Loc.GetString("uplink-spoofer-scanned", ("prints", ent.Comp.Fingerprints.Count), ("fibers", ent.Comp.Fibers.Count)), ent, args.User);
            return;
        }

        var targetForensics = EnsureComp<ForensicsComponent>(target);
        targetForensics.Fingerprints.UnionWith(ent.Comp.Fingerprints);
        targetForensics.Fibers.UnionWith(ent.Comp.Fibers);
        _popup.PopupEntity(Loc.GetString("uplink-spoofer-applied", ("target", target)), ent, args.User);
    }

    #endregion

    #region Радиоактивный микролазер

    private void OnRadLaserExamined(Entity<UplinkRadLaserComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("uplink-rad-laser-settings", ("intensity", ent.Comp.Intensity), ("wavelength", ent.Comp.Wavelength)));
    }

    private void OnRadLaserVerbs(Entity<UplinkRadLaserComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_player.TryGetSessionByEntity(args.User, out var session))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-rad-laser-verb"),
            Act = () =>
            {
                _dialog.OpenDialog<int, int>(session,
                    Loc.GetString("uplink-rad-laser-title"),
                    Loc.GetString("uplink-rad-laser-intensity"),
                    Loc.GetString("uplink-rad-laser-wavelength"),
                    (intensity, wavelength) =>
                    {
                        ent.Comp.Intensity = Math.Clamp(intensity, 1, 20);
                        ent.Comp.Wavelength = Math.Clamp(wavelength, 0, 120);
                        _popup.PopupEntity(Loc.GetString("uplink-rad-laser-settings", ("intensity", ent.Comp.Intensity), ("wavelength", ent.Comp.Wavelength)), ent, user);
                    });
            },
        });
    }

    private void OnRadLaserInteract(Entity<UplinkRadLaserComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;

        if (_timing.CurTime < ent.Comp.ReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("uplink-rad-laser-recharging"), ent, args.User);
            return;
        }

        // get_cooldown: max(10, intensity * 5 - wavelength / 4) секунд.
        var cooldown = Math.Max(10, ent.Comp.Intensity * 5 - ent.Comp.Wavelength / 4);
        ent.Comp.ReadyAt = _timing.CurTime + TimeSpan.FromSeconds(cooldown);

        var victim = EnsureComp<UplinkRadLaserVictimComponent>(target);
        victim.Intensity = ent.Comp.Intensity;
        // (intensity + wavelength * 4) * 5 децисекунд.
        victim.TriggerAt = _timing.CurTime + TimeSpan.FromSeconds((ent.Comp.Intensity + ent.Comp.Wavelength * 4) * 0.5f);

        _popup.PopupEntity(Loc.GetString("uplink-rad-laser-hit", ("target", target)), ent, args.User);
    }

    #endregion

    #region Экспериментальный телепортер

    private void OnTeleporterExamined(Entity<UplinkSyndicateTeleporterComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-teleporter-charges", ("charges", ent.Comp.Charges), ("max", ent.Comp.MaxCharges)));
    }

    private void OnTeleporterUse(Entity<UplinkSyndicateTeleporterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        AttemptTeleport(ent, args.User, false);
    }

    private void OnTeleporterEmp(Entity<UplinkSyndicateTeleporterComponent> ent, ref EmpPulseEvent args)
    {
        if (!_random.Prob(0.5f))
            return;

        var holder = Transform(ent).ParentUid;
        if (HasComp<MobStateComponent>(holder))
            AttemptTeleport(ent, holder, true);
    }

    /// <summary>attempt_teleport: прыжок на 4–8 тайлов вперёд; в стене — аварийный параллельный прыжок, иначе гибель.</summary>
    private void AttemptTeleport(Entity<UplinkSyndicateTeleporterComponent> ent, EntityUid user, bool emp)
    {
        if (ent.Comp.Charges <= 0 && !emp)
        {
            _popup.PopupEntity(Loc.GetString("uplink-teleporter-recharging"), user, user);
            return;
        }

        var xform = Transform(user);
        if (xform.GridUid == null && xform.MapUid == null)
            return;

        var distance = _random.Next(ent.Comp.MinDistance, ent.Comp.MaxDistance + 1);
        var direction = _transform.GetWorldRotation(user).ToWorldVec();
        var origin = _transform.GetMapCoordinates(user);
        var destination = origin.Offset(direction * distance);

        if (IsBlocked(destination))
        {
            if (emp || !TryPanicTeleport(destination, direction, ent.Comp.ParallelDistance, out var safe))
            {
                GetFragged(user, destination);
                return;
            }

            destination = safe;
            _popup.PopupEntity(Loc.GetString("uplink-teleporter-emergency"), user, user, PopupType.MediumCaution);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg"), ent);
        }

        Telefrag(destination, user);
        _transform.SetMapCoordinates(user, destination);
        ent.Comp.Charges = Math.Max(ent.Comp.Charges - 1, 0);

        // Кровь при телепорте: bleed_amount = 20 из 560.
        var blood = new DamageSpecifier();
        blood.DamageDict["Bloodloss"] = 3;
        _damageable.TryChangeDamage(user, blood, true);

        _audio.PlayPvs(TeleportSound, user);
        _audio.PlayPvs(PhaseInSound, user);
    }

    private bool TryPanicTeleport(MapCoordinates destination, Vector2 direction, int range, out MapCoordinates safe)
    {
        var perpendicular = new Vector2(-direction.Y, direction.X);
        for (var i = 1; i <= range; i++)
        {
            foreach (var sign in new[] { 1, -1 })
            {
                var candidate = destination.Offset(perpendicular * (i * sign));
                if (!IsBlocked(candidate))
                {
                    safe = candidate;
                    return true;
                }
            }
        }

        safe = destination;
        return false;
    }

    private bool IsBlocked(MapCoordinates coords)
    {
        if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _mapSystem.GetTileRef(gridUid, grid, coords);
        return _turf.IsTileBlocked(tile, CollisionGroup.Impassable);
    }

    /// <summary>telefrag: всех на тайле назначения ранит и опрокидывает.</summary>
    private void Telefrag(MapCoordinates destination, EntityUid user)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(destination, 0.4f))
        {
            if (mob.Owner == user)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 20;
            _damageable.TryChangeDamage(mob.Owner, damage, true);
            _stun.TryKnockdown(mob.Owner, TimeSpan.FromSeconds(4));
        }
    }

    /// <summary>get_fragged: взрыв в точке назначения и гибель.</summary>
    private void GetFragged(EntityUid user, MapCoordinates destination)
    {
        _transform.SetMapCoordinates(user, destination);
        _popup.PopupEntity(Loc.GetString("uplink-teleporter-fragged"), user, PopupType.LargeCaution);
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 300;
        _damageable.TryChangeDamage(user, damage, true);
        _explosion.QueueExplosion(destination, "Default", 20f, 5f, 5f, user, canCreateVacuum: false);
    }

    #endregion

    #region Взломанный модуль законов

    private void OnHackedModuleExamined(Entity<UplinkHackedLawModuleComponent> ent, ref ExaminedEvent args)
    {
        if (!string.IsNullOrWhiteSpace(ent.Comp.Law))
            args.PushMarkup(Loc.GetString("uplink-hacked-module-law", ("law", ent.Comp.Law)));
    }

    private void OnHackedModuleUse(Entity<UplinkHackedLawModuleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_player.TryGetSessionByEntity(args.User, out var session))
            return;

        args.Handled = true;
        var user = args.User;
        _dialog.OpenDialog<LongString>(session,
            Loc.GetString("uplink-hacked-module-title"),
            Loc.GetString("uplink-hacked-module-prompt"),
            law =>
            {
                ent.Comp.Law = law.String.Trim();
                _popup.PopupEntity(Loc.GetString("uplink-hacked-module-set"), ent, user);
            });
    }

    /// <summary>
    /// Модуль загружает приоритетный закон 0 поверх текущих законов (hacked.dm), а не заменяет их.
    /// Перед вставкой в консоль подменяем набор законов платы на «текущие + новый».
    /// </summary>
    private void OnHackedModuleInsertAttempt(Entity<UplinkHackedLawModuleComponent> ent, ref ContainerGettingInsertedAttemptEvent args)
    {
        if (args.Cancelled || !TryComp<SiliconLawUpdaterComponent>(args.Container.Owner, out var updater))
            return;

        // Без записанного закона модуль не вставить в консоль загрузки законов.
        if (string.IsNullOrWhiteSpace(ent.Comp.Law))
        {
            args.Cancel();
            return;
        }

        // Запоминаем текущие законы: ванильная консоль сейчас заменит их набором платы,
        // а на следующем тике мы вернём их и добавим приоритетный закон 0.
        var query = EntityManager.CompRegistryQueryEnumerator(updater.Components);
        while (query.MoveNext(out var target))
        {
            var laws = _laws.GetLaws(target).Laws.Select(l => l.ShallowClone()).ToList();
            laws.RemoveAll(l => l.Order == 0);
            laws.Add(new SiliconLaw { LawString = ent.Comp.Law, Order = 0 });
            _pendingLaws.Add((target, laws));
        }
    }

    private readonly List<(EntityUid Target, List<SiliconLaw> Laws)> _pendingLaws = new();

    private void ApplyPendingLaws()
    {
        if (_pendingLaws.Count == 0)
            return;

        foreach (var (target, laws) in _pendingLaws)
        {
            if (!TerminatingOrDeleted(target))
                _laws.SetLaws(laws, target, new SoundPathSpecifier("/Audio/Misc/cryo_warning.ogg"));
        }

        _pendingLaws.Clear();
    }

    #endregion

    #region Картонные фигуры

    private void OnCutoutStartup(Entity<UplinkCardboardCutoutComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.States.Count > 0)
            _appearance.SetData(ent, UplinkCutoutVisuals.State, ent.Comp.States[ent.Comp.Index % ent.Comp.States.Count]);
    }

    /// <summary>Мелком меняется облик фигуры.</summary>
    private void OnCutoutInteract(Entity<UplinkCardboardCutoutComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.States.Count == 0 || !HasComp<Content.Shared.Crayon.CrayonComponent>(args.Used))
            return;

        args.Handled = true;
        ent.Comp.Index = (ent.Comp.Index + 1) % ent.Comp.States.Count;
        Dirty(ent);
        _appearance.SetData(ent, UplinkCutoutVisuals.State, ent.Comp.States[ent.Comp.Index]);
    }

    #endregion

    #region Гипноз

    /// <summary>hypnosis_vulnerable: спит, дремлет, пьян или под галлюциногенами.</summary>
    private bool IsHypnosisVulnerable(EntityUid uid)
    {
        return _status.HasStatusEffect(uid, SleepingSystem.StatusEffectForcedSleeping)
               || _status.HasStatusEffect(uid, "StatusEffectDrowsiness")
               || _status.HasStatusEffect(uid, "StatusEffectDrunk")
               || _status.HasStatusEffect(uid, "StatusEffectSeeingRainbow")
               || _status.HasStatusEffect(uid, "StatusEffectWoozy");
    }

    private void OnHypnoticFlashed(Entity<UplinkHypnoticFlashComponent> ent, ref AfterFlashedEvent args)
    {
        var target = args.Target;
        if (!HasComp<HumanoidProfileComponent>(target))
            return;

        if (!IsHypnosisVulnerable(target))
        {
            // Цель не уязвима: только растерянность и дрёма.
            _popup.PopupEntity(Loc.GetString("uplink-hypno-relaxed"), target, target, PopupType.Medium);
            _status.TryAddStatusEffectDuration(target, "StatusEffectDrowsiness", TimeSpan.FromSeconds(20));
            _status.TryAddStatusEffectDuration(target, "StatusEffectPacifism", TimeSpan.FromSeconds(4));
            return;
        }

        StartTrance(target);
    }

    private void OnHypnoticGrenade(Entity<UplinkHypnoticGrenadeComponent> ent, ref TriggerEvent args)
    {
        var coords = _transform.GetMapCoordinates(ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Uplink/screech.ogg"), ent);
        foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, ent.Comp.Range))
        {
            var attempt = new FlashAttemptEvent(mob, args.User, ent);
            RaiseLocalEvent(mob, ref attempt, true);
            if (attempt.Cancelled)
                continue;

            StartTrance(mob);
        }
    }

    /// <summary>Транс: следующая услышанная фраза становится навязчивой (brain_trauma/hypnosis).</summary>
    public void StartTrance(EntityUid target)
    {
        var trance = EnsureComp<UplinkHypnoticTranceComponent>(target);
        trance.EndTime = _timing.CurTime + TimeSpan.FromSeconds(60);
        EnsureComp<ActiveListenerComponent>(target).Range = 7f;
        _status.TryAddStatusEffectDuration(target, "StatusEffectWoozy", TimeSpan.FromSeconds(60));
        _popup.PopupEntity(Loc.GetString("uplink-hypno-trance"), target, target, PopupType.LargeCaution);
    }

    private void OnTranceListenAttempt(Entity<UplinkHypnoticTranceComponent> ent, ref ListenAttemptEvent args)
    {
        if (args.Source == ent.Owner)
            args.Cancel();
    }

    private void OnTranceListen(Entity<UplinkHypnoticTranceComponent> ent, ref ListenEvent args)
    {
        if (args.Source == ent.Owner || string.IsNullOrWhiteSpace(args.Message))
            return;

        var hypno = EnsureComp<UplinkHypnotizedComponent>(ent);
        hypno.Phrase = args.Message;
        hypno.NextReminder = _timing.CurTime + TimeSpan.FromSeconds(60);

        RemCompDeferred<UplinkHypnoticTranceComponent>(ent);
        RemCompDeferred<ActiveListenerComponent>(ent);

        if (_player.TryGetSessionByEntity(ent, out var session))
        {
            var msg = Loc.GetString("uplink-hypno-phrase", ("phrase", FormattedMessageEscape(args.Message)));
            _chatManager.ChatMessageToOne(Content.Shared.Chat.ChatChannel.Emotes, msg, msg, ent, false, session.Channel, Color.Magenta);
        }
    }

    #endregion
}
