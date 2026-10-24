using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Jittering;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Shared.RCD.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Stunnable;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.VendingMachines;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>Оживлённая машина (mimic/copy/machine): облик берётся с прототипа машины.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfLockdownDoorComponent : Component;

/// <summary>Отложенное действие модуля (5 секунд «жужжания» перед оживлением или взрывом).</summary>
public sealed partial class ImperialMalfAiSystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly SharedAirlockSystem _airlocks = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedPoweredLightSystem _lights = default!;
    [Dependency] private readonly EmergencyLightSystem _emergency = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameras = default!;
    [Dependency] private readonly AirAlarmSystem _airAlarms = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    private static readonly TimeSpan LockdownDuration = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MachineDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RollTime = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan RollCooldown = TimeSpan.FromSeconds(1.5);
    private static readonly SoundSpecifier InterferenceSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");
    private static readonly SoundSpecifier AirhornSound = new SoundPathSpecifier("/Audio/Items/airhorn.ogg");
    private static readonly SoundSpecifier PhaseInSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
    private static readonly SoundSpecifier BangSound = new SoundPathSpecifier("/Audio/Effects/metal_crunch.ogg");
    private static readonly SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly EntProtoId MimicProto = "ImperialMalfMachineMimic";
    private static readonly EntProtoId FactoryProto = "ImperialMalfRoboticFactory";
    private static readonly EntProtoId MechProxyProto = "ImperialMalfMechProxy";
    private static readonly EntProtoId MechReturnAction = "ActionImperialMalfMechReturn";

    /// <summary>Отложенные «жужжания»: оживление и перегрузка срабатывают через 5 секунд.</summary>
    private readonly List<(TimeSpan At, EntityUid Ai, EntityUid Target, bool Explode)> _pendingMachines = new();

    private readonly List<(TimeSpan At, EntityUid Ai, Vector2 Direction)> _pendingRolls = new();
    private readonly List<(TimeSpan At, EntityUid Vendor, EntityCoordinates Target)> _pendingTilts = new();
    private TimeSpan? _lockdownEnd;
    private readonly Dictionary<EntityUid, TimeSpan> _rollCooldown = new();

    /// <summary>overload: счётчик перегрузок ЛКП для «Отключения света».</summary>
    private readonly Dictionary<EntityUid, int> _apcOverload = new();

    private void InitializeModules()
    {
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfModuleBoughtEvent>(OnModuleBought);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfLockdownActionEvent>(OnLockdown);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfOverrideMachineActionEvent>(OnOverrideMachine);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfOverloadMachineActionEvent>(OnOverloadMachine);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfDestroyRcdsActionEvent>(OnDestroyRcds);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfBlackoutActionEvent>(OnBlackout);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfHonkActionEvent>(OnHonk);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfFactoryActionEvent>(OnFactory);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfFloodActionEvent>(OnFlood);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfFireAlarmsActionEvent>(OnFireAlarms);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfEmergencyLightsActionEvent>(OnEmergencyLights);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfReactivateCamerasActionEvent>(OnReactivateCameras);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfEmagActionEvent>(OnEmag);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfRollActionEvent>(OnRoll);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfVendorTiltActionEvent>(OnVendorTilt);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfDominateMechActionEvent>(OnDominateMech);
        SubscribeLocalEvent<ImperialMalfMechProxyComponent, ImperialMalfMechReturnActionEvent>(OnMechReturn);
        InitializeUpgrades();
        InitializeDoomsday();
    }

    private bool OnStation(EntityUid ai, EntityUid target)
    {
        var station = _ai.TryGetCore(ai, out var core) ? _station.GetOwningStation(core.Owner) : null;
        return station == null || _station.GetOwningStation(target) == station;
    }

    private void PlayToAi(EntityUid ai, SoundSpecifier sound)
    {
        if (TryComp<ActorComponent>(ai, out var actor))
            _audio.PlayGlobal(sound, actor.PlayerSession);
    }

    #region Разрушительные

    /// <summary>lockdown: шлюзы закрыты, болтированы и под током 90 секунд.</summary>
    private void OnLockdown(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfLockdownActionEvent args)
    {
        if (args.Handled || !TryUseModule(ent, "ImperialMalfLockdown"))
            return;

        args.Handled = true;
        var query = EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var door, out var xform))
        {
            if (!OnStation(ent, uid))
                continue;

            if (TryComp<AirlockComponent>(uid, out var airlock))
            {
                // canAIControl(): провод ИИ цел, шлюз под питанием.
                if (!airlock.Powered || TryComp<StationAiWhitelistComponent>(uid, out var whitelist) && !whitelist.Enabled)
                    continue;

                _airlocks.SetSafety(airlock, false);
                if (TryComp<DoorBoltComponent>(uid, out var bolts))
                    _doors.SetBoltsDown((uid, bolts), false);
                _doors.TryClose(uid, door);
                if (TryComp<DoorBoltComponent>(uid, out bolts))
                    _doors.SetBoltsDown((uid, bolts), true);
                if (TryComp<ElectrifiedComponent>(uid, out var electrified))
                    _electrocution.SetElectrified((uid, electrified), true);
            }
            else
            {
                _doors.TryClose(uid, door);
            }

            EnsureComp<ImperialMalfLockdownDoorComponent>(uid);
        }

        _lockdownEnd = _timing.CurTime + LockdownDuration;
        _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-malf-lockdown-announce"),
            Loc.GetString("imperial-malf-lockdown-sender"), colorOverride: Color.Red);
        Notify(ent, Loc.GetString("imperial-malf-lockdown-self"));
    }

    /// <summary>_malf_ai_undo_lockdown(): снять болты и ток, открыть.</summary>
    private void UndoLockdown()
    {
        var query = EntityQueryEnumerator<ImperialMalfLockdownDoorComponent, DoorComponent>();
        var doors = new List<(EntityUid, DoorComponent)>();
        while (query.MoveNext(out var uid, out _, out var door))
        {
            doors.Add((uid, door));
        }

        foreach (var (uid, door) in doors)
        {
            RemComp<ImperialMalfLockdownDoorComponent>(uid);
            if (TryComp<AirlockComponent>(uid, out var airlock))
            {
                if (TryComp<DoorBoltComponent>(uid, out var bolts))
                    _doors.SetBoltsDown((uid, bolts), false);
                if (TryComp<ElectrifiedComponent>(uid, out var electrified))
                    _electrocution.SetElectrified((uid, electrified), false);
                _airlocks.SetSafety(airlock, true);
            }

            _doors.TryOpen(uid, door);
        }

        _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-malf-lockdown-end"),
            Loc.GetString("imperial-malf-lockdown-end-sender"), playSound: false, colorOverride: Color.Green);
    }

    private static bool IsMachine(IEntityManager entities, EntityUid uid)
    {
        return entities.HasComponent<ApcPowerReceiverComponent>(uid)
               && !entities.HasComponent<MobStateComponent>(uid)
               && !entities.HasComponent<StationAiCoreComponent>(uid);
    }

    /// <summary>override_machine: машина оживает и нападает через 5 секунд.</summary>
    private void OnOverrideMachine(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfOverrideMachineActionEvent args)
    {
        if (PickTarget(args.Target, e => IsMachine(EntityManager, e)) is not { } target)
        {
            if (!args.Handled)
                _popup.PopupEntity(Loc.GetString("imperial-malf-no-target"), ent, ent);
            return;
        }

        if (args.Handled)
            return;

        if (!IsMachine(EntityManager, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-only-machines"), ent, ent);
            return;
        }

        if (!TryUseModule(ent, "ImperialMalfOverrideMachine"))
            return;

        args.Handled = true;
        PlayToAi(ent, InterferenceSound);
        _popup.PopupEntity(Loc.GetString("imperial-malf-machine-buzz", ("machine", Name(target))), target, PopupType.LargeCaution);
        _pendingMachines.Add((_timing.CurTime + MachineDelay, ent, target, false));
    }

    /// <summary>overload_machine: взрыв (2/3) через 5 секунд.</summary>
    private void OnOverloadMachine(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfOverloadMachineActionEvent args)
    {
        if (PickTarget(args.Target, e => IsMachine(EntityManager, e)) is not { } target)
        {
            if (!args.Handled)
                _popup.PopupEntity(Loc.GetString("imperial-malf-no-target"), ent, ent);
            return;
        }

        if (args.Handled)
            return;

        if (!IsMachine(EntityManager, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-only-machines"), ent, ent);
            return;
        }

        if (!TryUseModule(ent, "ImperialMalfOverloadMachine"))
            return;

        args.Handled = true;
        PlayToAi(ent, InterferenceSound);
        _popup.PopupEntity(Loc.GetString("imperial-malf-machine-buzz", ("machine", Name(target))), target, PopupType.LargeCaution);
        _pendingMachines.Add((_timing.CurTime + MachineDelay, ent, target, true));
    }

    private void FinishMachine(EntityUid ai, EntityUid target, bool explode)
    {
        if (TerminatingOrDeleted(target))
            return;

        var coords = _transform.GetMapCoordinates(target);
        if (explode)
        {
            Log.Info($"{ToPrettyString(ai)} перегрузил {ToPrettyString(target)}");
            _explosion.QueueExplosion(coords, ExplosionSystem.DefaultExplosionPrototypeId, 40, 5, 10, target);
            QueueDel(target);
            return;
        }

        // mimic/copy/machine: моб в облике машины.
        var mimic = Spawn(MimicProto, coords);
        if (MetaData(target).EntityPrototype is { } proto)
        {
            var comp = EnsureComp<ImperialMalfMimicComponent>(mimic);
            comp.Proto = proto.ID;
            Dirty(mimic, comp);
        }

        _meta.SetEntityName(mimic, Name(target));
        QueueDel(target);
    }

    /// <summary>destroy_rcds: взорвать все РЦД станции, кроме борговых.</summary>
    private void OnDestroyRcds(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfDestroyRcdsActionEvent args)
    {
        if (args.Handled || !TryUseModule(ent, "ImperialMalfDestroyRcds"))
            return;

        args.Handled = true;
        var query = EntityQueryEnumerator<RCDComponent, TransformComponent>();
        var targets = new List<EntityUid>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (HasComp<BorgChassisComponent>(xform.ParentUid) || HasComp<BorgModuleComponent>(xform.ParentUid))
                continue;

            targets.Add(uid);
        }

        foreach (var rcd in targets)
        {
            _explosion.QueueExplosion(_transform.GetMapCoordinates(rcd), ExplosionSystem.DefaultExplosionPrototypeId, 10, 3, 5, rcd);
            QueueDel(rcd);
        }

        Notify(ent, Loc.GetString("imperial-malf-rcd-done"));
        PlayToAi(ent, BeepSound);
    }

    /// <summary>blackout: каждый ЛКП — шанс 30% × перегрузки разбить лампы, иначе перегрузка растёт.</summary>
    private void OnBlackout(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfBlackoutActionEvent args)
    {
        if (args.Handled || !TryUseModule(ent, "ImperialMalfBlackout"))
            return;

        args.Handled = true;
        var query = EntityQueryEnumerator<ApcPowerProviderComponent>();
        while (query.MoveNext(out var apc, out var provider))
        {
            if (!OnStation(ent, apc))
                continue;

            var overload = _apcOverload.GetValueOrDefault(apc);
            if (_random.Prob(Math.Min(1f, 0.3f * overload)))
            {
                foreach (var receiver in provider.LinkedReceivers.ToList())
                {
                    var light = receiver.Owner;
                    if (HasComp<PoweredLightComponent>(light) && _random.Prob(0.7f))
                        _lights.TryDestroyBulb(light);
                }
            }
            else
            {
                _apcOverload[apc] = overload + 1;
            }
        }

        Notify(ent, Loc.GetString("imperial-malf-blackout-done"));
    }

    /// <summary>honk: интеркомы воют, живые в 6 клетках — оглушение и дрожь.</summary>
    private void OnHonk(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfHonkActionEvent args)
    {
        if (args.Handled || !TryUseModule(ent, "ImperialMalfMegahonk"))
            return;

        args.Handled = true;
        Notify(ent, Loc.GetString("imperial-malf-honk-self"));
        var victims = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<IntercomComponent, TransformComponent>();
        while (query.MoveNext(out var intercom, out _, out var xform))
        {
            if (!OnStation(ent, intercom))
                continue;

            _audio.PlayPvs(AirhornSound, intercom, AudioParams.Default.WithVolume(6));
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, 6f))
            {
                if (HasComp<BorgChassisComponent>(mob) || HasComp<StationAiHeldComponent>(mob) || !_mobs.IsAlive(mob))
                    continue;

                victims.Add(mob);
            }
        }

        foreach (var victim in victims)
        {
            _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(2));
            _jitter.DoJitter(victim, TimeSpan.FromSeconds(120), true);
            _popup.PopupEntity(Loc.GetString("imperial-malf-honk-victim"), victim, victim, PopupType.LargeCaution);
        }
    }

    #endregion

    #region Служебные

    /// <summary>place_transformer: машина 3×1 на виду у камер, превращает людей в киборгов. Перенос в ЛКП после неё невозможен.</summary>
    private void OnFactory(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfFactoryActionEvent args)
    {
        if (args.Handled || !_ai.TryGetCore(ent, out var core) || core.Comp?.RemoteEntity is not { } eye)
            return;

        var coords = Transform(eye).Coordinates;
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(gridUid, out var grid))
            return;

        var center = _maps.LocalToTile(gridUid, grid, coords);
        foreach (var offset in new[] { -1, 0, 1 })
        {
            var tile = center + new Vector2i(offset, 0);
            if (!_maps.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            {
                _popup.PopupEntity(Loc.GetString("imperial-malf-factory-space"), ent, ent);
                return;
            }

            foreach (var anchored in _maps.GetAnchoredEntities(gridUid, grid, tile))
            {
                if (TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(anchored, out var physics) && physics.Hard)
                {
                    _popup.PopupEntity(Loc.GetString("imperial-malf-factory-blocked"), ent, ent);
                    return;
                }
            }
        }

        if (!TryUseModule(ent, "ImperialMalfRoboticFactory"))
            return;

        args.Handled = true;
        var factory = Spawn(FactoryProto, _maps.GridTileToLocal(gridUid, grid, center));
        EnsureComp<ImperialMalfFactoryComponent>(factory).Ai = ent;
        _audio.PlayPvs(PhaseInSound, factory);
        ent.Comp.CanShunt = false;
        if (ent.Comp.ShuntAction != null)
        {
            _actions.RemoveAction(ent.Owner, ent.Comp.ShuntAction);
            ent.Comp.ShuntAction = null;
        }

        Notify(ent, Loc.GetString("imperial-malf-factory-no-shunt"));
    }

    /// <summary>break_air_alarms → режим затопления: вентиляция без проверок давления, скрубберы выключены.</summary>
    private void OnFlood(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfFloodActionEvent args)
    {
        if (PickTarget(args.Target, HasComp<AirAlarmComponent>) is not { } target)
        {
            if (!args.Handled)
                _popup.PopupEntity(Loc.GetString("imperial-malf-no-target"), ent, ent);
            return;
        }

        if (args.Handled || !TryComp<AirAlarmComponent>(target, out var alarm))
            return;

        args.Handled = true;
        var flood = new GasVentPumpData
        {
            Enabled = true,
            Dirty = true,
            PumpDirection = VentPumpDirection.Releasing,
            PressureChecks = VentPressureBound.NoBound,
            ExternalPressureBound = Atmospherics.MaxOutputPressure,
            InternalPressureBound = 0f,
            PressureLockoutOverride = true,
        };

        foreach (var address in alarm.VentData.Keys.ToList())
        {
            _airAlarms.SetData(target, address, flood);
        }

        foreach (var address in alarm.ScrubberData.Keys.ToList())
        {
            _airAlarms.SetData(target, address, GasVentScrubberData.FillModePreset);
        }

        EnsureComp<EmaggedComponent>(target);
        Notify(ent, Loc.GetString("imperial-malf-flood-done", ("alarm", Name(target))));
    }

    /// <summary>break_fire_alarms: пожарные тревоги больше не замечают огня, пожарные шлюзы не закрываются сами.</summary>
    private void OnFireAlarms(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfFireAlarmsActionEvent args)
    {
        if (args.Handled || !TryUseModule(ent, "ImperialMalfFireAlarms"))
            return;

        args.Handled = true;
        var alarms = EntityQueryEnumerator<FireAlarmComponent, AtmosAlarmableComponent>();
        while (alarms.MoveNext(out var uid, out _, out var alarmable))
        {
            if (OnStation(ent, uid))
                alarmable.IgnoreAlarms = true;
        }

        var firelocks = EntityQueryEnumerator<FirelockComponent, DoorComponent>();
        while (firelocks.MoveNext(out var uid, out var firelock, out var door))
        {
            if (!OnStation(ent, uid))
                continue;

            firelock.AlarmAutoClose = false;
            _doors.TryOpen(uid, door);
        }

        Notify(ent, Loc.GetString("imperial-malf-fire-alarms-done"));
    }

    /// <summary>emergency_lights: аварийный свет станции гаснет.</summary>
    private void OnEmergencyLights(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfEmergencyLightsActionEvent args)
    {
        if (args.Handled || !TryUseModule(ent, "ImperialMalfEmergencyLights"))
            return;

        args.Handled = true;
        var query = EntityQueryEnumerator<EmergencyLightComponent>();
        var lights = new List<EntityUid>();
        while (query.MoveNext(out var uid, out _))
        {
            if (OnStation(ent, uid))
                lights.Add(uid);
        }

        foreach (var light in lights)
        {
            RemComp<EmergencyLightComponent>(light);
            _lights.SetState(light, false);
        }

        Notify(ent, Loc.GetString("imperial-malf-emergency-lights-done"));
    }

    /// <summary>reactivate_cameras: каждая включённая камера тратит одно применение из 30.</summary>
    private void OnReactivateCameras(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfReactivateCamerasActionEvent args)
    {
        if (args.Handled || !ent.Comp.Modules.TryGetValue("ImperialMalfReactivateCameras", out var entry))
            return;

        args.Handled = true;
        var fixedCount = 0;
        var query = EntityQueryEnumerator<SurveillanceCameraComponent>();
        while (query.MoveNext(out var uid, out var camera) && entry.Uses > 0)
        {
            if (camera.Active || !OnStation(ent, uid))
                continue;

            _cameras.SetActive(uid, true, camera);
            if (!camera.Active)
                continue;

            fixedCount++;
            entry.Uses--;
        }

        ent.Comp.Modules["ImperialMalfReactivateCameras"] = entry;
        Notify(ent, Loc.GetString("imperial-malf-cameras-fixed", ("count", fixedCount), ("uses", entry.Uses)));
        if (entry.Uses <= 0)
        {
            _actions.RemoveAction(ent.Owner, entry.Action);
            ent.Comp.Modules.Remove("ImperialMalfReactivateCameras");
        }
    }

    /// <summary>emag: удалённо снять предохранители (до 7 раз).</summary>
    private void OnEmag(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfEmagActionEvent args)
    {
        if (PickTarget(args.Target, e => IsMachine(EntityManager, e)) is not { } target)
        {
            if (!args.Handled)
                _popup.PopupEntity(Loc.GetString("imperial-malf-no-target"), ent, ent);
            return;
        }

        if (args.Handled)
            return;

        var ev = new GotEmaggedEvent(ent, EmagType.Interaction | EmagType.Access);
        RaiseLocalEvent(target, ref ev);
        if (!ev.Handled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-emag-failed"), ent, ent);
            return;
        }

        if (!ev.Repeatable)
        {
            var emagged = EnsureComp<EmaggedComponent>(target);
            emagged.EmagType |= EmagType.Interaction | EmagType.Access;
            Dirty(target, emagged);
        }

        args.Handled = true;
        TryUseModule(ent, "ImperialMalfEmag");
        Notify(ent, Loc.GetString("imperial-malf-emag-done"));
    }

    /// <summary>core_tilt: ядро перекатывается на соседнюю клетку и давит всё живое (75 урона, 6 с паралича).</summary>
    private void OnRoll(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfRollActionEvent args)
    {
        if (args.Handled || !_ai.TryGetCore(ent, out var core))
            return;

        if (_rollCooldown.TryGetValue(ent, out var until) && _timing.CurTime < until)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-roll-cooldown"), ent, ent);
            return;
        }

        var from = _transform.GetWorldPosition(core.Owner);
        var to = _transform.ToMapCoordinates(args.Target).Position;
        var delta = to - from;
        if (delta.LengthSquared() < 0.25f)
            return;

        var direction = (Vector2) Angle.FromWorldVec(delta).GetDir().ToIntVec();
        if (!TryUseModule(ent, "ImperialMalfCoreTilt"))
            return;

        args.Handled = true;
        _rollCooldown[ent] = _timing.CurTime + RollTime + RollCooldown;
        _pendingRolls.Add((_timing.CurTime + RollTime, ent, direction));
        _popup.PopupEntity(Loc.GetString("imperial-malf-rolling"), core.Owner, PopupType.MediumCaution);
    }

    private void FinishRoll(EntityUid ai, Vector2 direction)
    {
        if (!_ai.TryGetCore(ai, out var core) || TerminatingOrDeleted(core.Owner))
            return;

        var xform = Transform(core.Owner);
        var target = xform.Coordinates.Offset(direction);
        Crush(target, core.Owner);
        var wasAnchored = xform.Anchored;
        if (wasAnchored)
            _transform.Unanchor(core.Owner, xform);
        _transform.SetCoordinates(core.Owner, target);
        if (wasAnchored)
            _transform.AnchorEntity(core.Owner);
        _audio.PlayPvs(BangSound, core.Owner);
    }

    /// <summary>fall_and_crush(): 75 грубого урона и паралич всем на клетке.</summary>
    private void Crush(EntityCoordinates target, EntityUid source)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(target, 0.5f))
        {
            if (mob.Owner == source)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 75;
            _damage.TryChangeDamage(mob.Owner, damage, origin: source);
            _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(6));
        }
    }

    /// <summary>remote_vendor_tilt: автомат рядом с выбранной клеткой падает на неё.</summary>
    private void OnVendorTilt(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfVendorTiltActionEvent args)
    {
        if (args.Handled)
            return;

        EntityUid? vendor = null;
        foreach (var candidate in _lookup.GetEntitiesInRange<VendingMachineComponent>(args.Target, 1.5f))
        {
            if (!HasComp<ImperialMalfTiltedComponent>(candidate))
                vendor = candidate;
        }

        if (vendor is not { } found)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-vendor-none"), ent, ent);
            return;
        }

        if (!TryUseModule(ent, "ImperialMalfVendorTilt"))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-malf-vendor-falling", ("vendor", Name(found))), found, PopupType.LargeCaution);
        _pendingTilts.Add((_timing.CurTime + TimeSpan.FromSeconds(0.5), found, args.Target));
    }

    private void FinishTilt(EntityUid vendor, EntityCoordinates target)
    {
        if (TerminatingOrDeleted(vendor) || HasComp<ImperialMalfTiltedComponent>(vendor))
            return;

        var xform = Transform(vendor);
        var tile = _transform.ToCoordinates(_transform.ToMapCoordinates(target));
        Crush(tile, vendor);
        EnsureComp<ImperialMalfTiltedComponent>(vendor);
        var direction = _transform.ToMapCoordinates(target).Position - _transform.GetWorldPosition(vendor);
        if (xform.Anchored)
            _transform.Unanchor(vendor, xform);
        _transform.SetCoordinates(vendor, tile);
        _transform.SetWorldRotation(vendor, Angle.FromWorldVec(direction) + Angle.FromDegrees(90));
        _audio.PlayPvs(BangSound, vendor);
    }

    /// <summary>mecha_domination: пилот выброшен, ИИ управляет мехом через «пилота-призрака».</summary>
    private void OnDominateMech(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfDominateMechActionEvent args)
    {
        if (PickTarget(args.Target, HasComp<MechComponent>) is not { } target)
        {
            if (!args.Handled)
                _popup.PopupEntity(Loc.GetString("imperial-malf-no-target"), ent, ent);
            return;
        }

        if (args.Handled || !TryComp<MechComponent>(target, out var mech))
            return;

        if (!_mind.TryGetMind(ent, out var mindId, out var mind) || mind.VisitingEntity != null)
            return;

        args.Handled = true;
        _mech.TryEject(target, mech);
        var proxy = Spawn(MechProxyProto, _transform.GetMapCoordinates(target));
        if (!_mech.TryInsert(target, proxy, mech))
        {
            QueueDel(proxy);
            _popup.PopupEntity(Loc.GetString("imperial-malf-mech-failed"), ent, ent);
            return;
        }

        TryUseModule(ent, "ImperialMalfDominateMech");

        var comp = EnsureComp<ImperialMalfMechProxyComponent>(proxy);
        comp.Ai = ent;
        comp.Mech = target;
        comp.Mind = mindId;
        _meta.SetEntityName(proxy, Name(ent));
        comp.ReturnAction = _actions.AddAction(proxy, MechReturnAction);
        _mind.Visit(mindId, proxy, mind);
        Notify(ent, Loc.GetString("imperial-malf-mech-dominated", ("mech", Name(target))));
    }

    private void OnMechReturn(Entity<ImperialMalfMechProxyComponent> ent, ref ImperialMalfMechReturnActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        LeaveMech(ent);
    }

    private void LeaveMech(Entity<ImperialMalfMechProxyComponent> ent)
    {
        if (TryComp<Content.Shared.Mind.MindComponent>(ent.Comp.Mind, out var mind) && mind.VisitingEntity == ent.Owner)
            _mind.UnVisit(ent.Comp.Mind, mind);

        if (TryComp<MechComponent>(ent.Comp.Mech, out var mech) && mech.PilotSlot.ContainedEntity == ent.Owner)
            _mech.TryEject(ent.Comp.Mech, mech);

        QueueDel(ent);
    }

    #endregion

    /// <summary>Взломан ЛКП — скидка устройства Судного дня за командные отсеки.</summary>
    private void OnApcHacked(Entity<ImperialMalfAiComponent> ent, EntityUid apc)
    {
        DoomsdayApcHacked(ent, apc);
    }

    private void UpdateModules(TimeSpan now)
    {
        for (var i = _pendingMachines.Count - 1; i >= 0; i--)
        {
            var pending = _pendingMachines[i];
            if (now < pending.At)
                continue;

            _pendingMachines.RemoveAt(i);
            FinishMachine(pending.Ai, pending.Target, pending.Explode);
        }

        for (var i = _pendingRolls.Count - 1; i >= 0; i--)
        {
            var pending = _pendingRolls[i];
            if (now < pending.At)
                continue;

            _pendingRolls.RemoveAt(i);
            FinishRoll(pending.Ai, pending.Direction);
        }

        for (var i = _pendingTilts.Count - 1; i >= 0; i--)
        {
            var pending = _pendingTilts[i];
            if (now < pending.At)
                continue;

            _pendingTilts.RemoveAt(i);
            FinishTilt(pending.Vendor, pending.Target);
        }

        if (_lockdownEnd is { } end && now >= end)
        {
            _lockdownEnd = null;
            UndoLockdown();
        }

        // Мех уничтожен или ИИ погиб — разум возвращается в ядро.
        var proxies = EntityQueryEnumerator<ImperialMalfMechProxyComponent>();
        var leaving = new List<Entity<ImperialMalfMechProxyComponent>>();
        while (proxies.MoveNext(out var uid, out var proxy))
        {
            if (TerminatingOrDeleted(proxy.Mech) || TerminatingOrDeleted(proxy.Ai)
                || !TryComp<MechComponent>(proxy.Mech, out var mech) || mech.PilotSlot.ContainedEntity != uid)
                leaving.Add((uid, proxy));
        }

        foreach (var proxy in leaving)
        {
            LeaveMech(proxy);
        }

        UpdateFactories();
        UpdateUpgrades();
        UpdateDoomsday(now);
    }

    private void OnModuleBought(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfModuleBoughtEvent args)
    {
        ApplyUpgrade(ent, args.Module);
    }
}

/// <summary>Автомат уже опрокинут.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfTiltedComponent : Component;

/// <summary>«Пилот-призрак» меха под управлением ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfMechProxyComponent : Component
{
    [ViewVariables]
    public EntityUid Ai;

    [ViewVariables]
    public EntityUid Mech;

    [ViewVariables]
    public EntityUid Mind;

    [ViewVariables]
    public EntityUid? ReturnAction;
}

/// <summary>Роботизированная фабрика (transformer): кладёт людей и превращает в киборгов ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfFactoryComponent : Component
{
    [ViewVariables]
    public EntityUid Ai;

    /// <summary>Перезарядка между превращениями (cooldown_duration 60 с у TG).</summary>
    [ViewVariables]
    public TimeSpan NextUse;
}
