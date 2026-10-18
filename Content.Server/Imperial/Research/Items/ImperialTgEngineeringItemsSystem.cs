using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Components;
using Content.Server.Imperial.Sanity.Components;
using Content.Server.Imperial.Sanity.Systems;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Research.Items;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Projectiles;
using Content.Shared.Trigger.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Инженерные предметы из исследований TG: пневмозатвор, болтерный ключ, дальнобойный газоанализатор,
/// датчик здоровья, индукторы, портативная помпа, генератор атмосферного щита и лучи дисков эмиттера.
/// </summary>
public sealed class ImperialTgEngineeringItemsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly Robust.Shared.Map.IMapManager _mapManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SanitySystem _sanity = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialPneumaticSealComponent, AfterInteractEvent>(OnSealInteract);
        SubscribeLocalEvent<ImperialPneumaticSealComponent, ImperialSealDoAfterEvent>(OnSealDoAfter);
        SubscribeLocalEvent<ImperialSealedDoorComponent, GetVerbsEvent<AlternativeVerb>>(OnSealedDoorVerbs);
        SubscribeLocalEvent<ImperialSealedDoorComponent, ImperialUnsealDoAfterEvent>(OnUnsealDoAfter);
        SubscribeLocalEvent<ImperialSealedDoorComponent, ExaminedEvent>(OnSealedExamined);

        SubscribeLocalEvent<ImperialBolterWrenchComponent, AfterInteractEvent>(OnBolterInteract);
        SubscribeLocalEvent<ImperialBolterWrenchComponent, ImperialBolterWrenchDoAfterEvent>(OnBolterDoAfter);

        SubscribeLocalEvent<ImperialRangedGasAnalyzerComponent, AfterInteractEvent>(OnRangedAnalyzer);

        SubscribeLocalEvent<ImperialHealthSensorComponent, UseInHandEvent>(OnSensorUse);
        SubscribeLocalEvent<ImperialHealthSensorComponent, ExaminedEvent>(OnSensorExamined);

        SubscribeLocalEvent<ImperialInducerComponent, AfterInteractEvent>(OnInducerInteract);
        SubscribeLocalEvent<ImperialInducerComponent, ImperialInducerDoAfterEvent>(OnInducerDoAfter);

        SubscribeLocalEvent<ImperialPortablePumpComponent, AtmosDeviceUpdateEvent>(OnPumpUpdate);
        SubscribeLocalEvent<ImperialPortablePumpComponent, GetVerbsEvent<AlternativeVerb>>(OnPumpVerbs);
        SubscribeLocalEvent<ImperialPortablePumpComponent, ExaminedEvent>(OnPumpExamined);

        SubscribeLocalEvent<ImperialAtmosShieldGenComponent, ComponentShutdown>(OnShieldShutdown);

        SubscribeLocalEvent<ImperialEmitterBeamEffectsComponent, ProjectileHitEvent>(OnEmitterBeamHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var sensors = EntityQueryEnumerator<ImperialHealthSensorComponent>();
        while (sensors.MoveNext(out var uid, out var sensor))
            UpdateSensor(uid, sensor);

        var now = _timing.CurTime;
        var shields = EntityQueryEnumerator<ImperialAtmosShieldGenComponent, TransformComponent>();
        while (shields.MoveNext(out var uid, out var gen, out var xform))
        {
            if (now < gen.NextCheck)
                continue;

            gen.NextCheck = now + TimeSpan.FromSeconds(1);
            UpdateShield(uid, gen, xform);
        }
    }

    #region Пневмозатвор

    private void OnSealInteract(Entity<ImperialPneumaticSealComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } door || !TryComp<DoorComponent>(door, out var doorComp))
            return;

        args.Handled = true;

        if (HasComp<ImperialSealedDoorComponent>(door))
        {
            _popup.PopupEntity(Loc.GetString("imperial-seal-already"), door, args.User);
            return;
        }

        if (doorComp.State != DoorState.Closed)
        {
            _popup.PopupEntity(Loc.GetString("imperial-seal-must-close"), door, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.SealTime,
            new ImperialSealDoAfterEvent(), ent, target: door, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSealDoAfter(Entity<ImperialPneumaticSealComponent> ent, ref ImperialSealDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } door)
            return;

        args.Handled = true;
        var sealed_ = EnsureComp<ImperialSealedDoorComponent>(door);
        sealed_.Seal = MetaData(ent).EntityPrototype?.ID ?? sealed_.Seal;
        Dirty(door, sealed_);
        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString("imperial-seal-done", ("door", door)), door, args.User);
    }

    private void OnSealedExamined(Entity<ImperialSealedDoorComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-seal-examine"));
    }

    private void OnSealedDoorVerbs(Entity<ImperialSealedDoorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-seal-remove-verb"),
            Act = () =>
            {
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, 4f,
                    new ImperialUnsealDoAfterEvent(), ent, target: ent)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                });
            },
        });
    }

    private void OnUnsealDoAfter(Entity<ImperialSealedDoorComponent> ent, ref ImperialUnsealDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        Spawn(ent.Comp.Seal, _transform.GetMapCoordinates(args.User));
        RemComp<ImperialSealedDoorComponent>(ent);
        _popup.PopupEntity(Loc.GetString("imperial-seal-removed", ("door", ent.Owner)), ent, args.User);
    }

    #endregion

    #region Болтерный ключ

    private void OnBolterInteract(Entity<ImperialBolterWrenchComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } door || !HasComp<DoorBoltComponent>(door))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new ImperialBolterWrenchDoAfterEvent(), ent, target: door, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBolterDoAfter(Entity<ImperialBolterWrenchComponent> ent, ref ImperialBolterWrenchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } door || !TryComp<DoorBoltComponent>(door, out var bolts))
            return;

        args.Handled = true;
        _door.SetBoltsDown((door, bolts), !bolts.BoltsDown, args.User);
        _popup.PopupEntity(Loc.GetString(bolts.BoltsDown ? "imperial-bolter-down" : "imperial-bolter-up"), door, args.User);
    }

    #endregion

    #region Дальнобойный газоанализатор

    private void OnRangedAnalyzer(Entity<ImperialRangedGasAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        var from = _transform.GetMapCoordinates(args.User);
        var to = _transform.ToMapCoordinates(args.ClickLocation);
        if (from.MapId != to.MapId || (from.Position - to.Position).Length() > ent.Comp.Range)
            return;

        args.Handled = true;

        GasMixture? mix = null;
        if (_mapManager.TryFindGridAt(to, out var gridUid, out var grid))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, to);
            mix = _atmos.GetTileMixture(gridUid, null, tile);
        }

        if (mix == null || mix.TotalMoles <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ranged-analyzer-vacuum"), args.User, args.User);
            return;
        }

        var lines = new List<string>
        {
            Loc.GetString("imperial-ranged-analyzer-header",
                ("pressure", $"{mix.Pressure:0.##}"),
                ("temperature", $"{mix.Temperature - Atmospherics.T0C:0.#}")),
        };

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var moles = mix.GetMoles(i);
            if (moles <= 0.01f)
                continue;

            var gas = _atmos.GetGas(i);
            lines.Add($"{Loc.GetString(gas.Name)}: {moles / mix.TotalMoles * 100:0.#}%");
        }

        _popup.PopupEntity(string.Join("\n", lines), args.User, args.User);
    }

    #endregion

    #region Датчик здоровья

    private void OnSensorUse(Entity<ImperialHealthSensorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.WaitForDeath = !ent.Comp.WaitForDeath;
        ent.Comp.Triggered = false;
        _popup.PopupEntity(Loc.GetString(ent.Comp.WaitForDeath ? "imperial-health-sensor-death" : "imperial-health-sensor-crit"), ent, args.User);
    }

    private void OnSensorExamined(Entity<ImperialHealthSensorComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.WaitForDeath ? "imperial-health-sensor-death" : "imperial-health-sensor-crit"));
    }

    private void UpdateSensor(EntityUid uid, ImperialHealthSensorComponent sensor)
    {
        if (!sensor.Scanning)
            return;

        // Носитель — ближайший внешний контейнер с состоянием моба (карман, рука, сборка в руке).
        EntityUid? holder = null;
        var current = uid;
        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            current = container.Owner;
            if (HasComp<MobStateComponent>(current))
            {
                holder = current;
                break;
            }
        }

        if (holder == null || !TryComp<MobStateComponent>(holder, out var state))
            return;

        var bad = sensor.WaitForDeath
            ? state.CurrentState == MobState.Dead
            : state.CurrentState is MobState.Critical or MobState.Dead;

        if (!bad)
        {
            sensor.Triggered = false;
            return;
        }

        if (sensor.Triggered)
            return;

        sensor.Triggered = true;
        _trigger.Trigger(uid, holder);
    }

    #endregion

    #region Индуктор

    private void OnInducerInteract(Entity<ImperialInducerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == ent.Owner)
            return;

        if (!_cell.TryGetBatteryFromEntityOrSlot(target, out _))
            return;

        args.Handled = true;
        StartInduce(ent, args.User, target);
    }

    private void StartInduce(Entity<ImperialInducerComponent> ent, EntityUid user, EntityUid target)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay,
            new ImperialInducerDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnInducerDoAfter(Entity<ImperialInducerComponent> ent, ref ImperialInducerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (!_cell.TryGetBatteryFromEntityOrSlot(ent.Owner, out var source) ||
            !_cell.TryGetBatteryFromEntityOrSlot(target, out var dest))
        {
            _popup.PopupEntity(Loc.GetString("imperial-inducer-no-cell"), ent, args.User);
            return;
        }

        var free = dest.Value.Comp.MaxCharge - _battery.GetCharge(dest.Value.AsNullable());
        var have = _battery.GetCharge(source.Value.AsNullable());
        var amount = MathF.Min(ent.Comp.Transfer, MathF.Min(free, have));
        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString(free <= 0 ? "imperial-inducer-full" : "imperial-inducer-empty"), ent, args.User);
            return;
        }

        _battery.ChangeCharge(source.Value.AsNullable(), -amount);
        _battery.ChangeCharge(dest.Value.AsNullable(), amount);
        args.Repeat = true;
    }

    #endregion

    #region Портативная помпа

    private void OnPumpUpdate(Entity<ImperialPortablePumpComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;
        if (!comp.Enabled || !_power.IsPowered(ent.Owner))
            return;

        var env = _atmos.GetContainingMixture(ent.Owner, args.Grid, args.Map, excite: true);
        if (env == null)
            return;

        if (comp.PumpOut)
            _atmos.PumpGasTo(comp.Air, env, comp.TargetPressure);
        else
            _atmos.PumpGasTo(env, comp.Air, MathF.Min(comp.TargetPressure, comp.MaxPressure));
    }

    private void OnPumpVerbs(Entity<ImperialPortablePumpComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Enabled ? "imperial-pump-turn-off" : "imperial-pump-turn-on"),
            Priority = 3,
            Act = () =>
            {
                ent.Comp.Enabled = !ent.Comp.Enabled;
                _popup.PopupEntity(Loc.GetString(ent.Comp.Enabled ? "imperial-pump-on" : "imperial-pump-off"), ent, user);
            },
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-pump-direction"),
            Priority = 2,
            Act = () =>
            {
                ent.Comp.PumpOut = !ent.Comp.PumpOut;
                _popup.PopupEntity(Loc.GetString(ent.Comp.PumpOut ? "imperial-pump-out" : "imperial-pump-in"), ent, user);
            },
        });
        foreach (var delta in new[] { 50f, -50f })
        {
            var d = delta;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("imperial-pump-pressure", ("delta", d)),
                Priority = 1,
                Act = () =>
                {
                    ent.Comp.TargetPressure = Math.Clamp(ent.Comp.TargetPressure + d, 0f, ent.Comp.MaxPressure);
                    _popup.PopupEntity(Loc.GetString("imperial-pump-target", ("pressure", $"{ent.Comp.TargetPressure:0}")), ent, user);
                },
            });
        }
    }

    private void OnPumpExamined(Entity<ImperialPortablePumpComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-pump-examine",
            ("state", Loc.GetString(ent.Comp.Enabled ? "imperial-pump-state-on" : "imperial-pump-state-off")),
            ("direction", Loc.GetString(ent.Comp.PumpOut ? "imperial-pump-out" : "imperial-pump-in")),
            ("target", $"{ent.Comp.TargetPressure:0}"),
            ("tank", $"{ent.Comp.Air.Pressure:0}")));
    }

    #endregion

    #region Генератор атмосферного щита

    private void UpdateShield(EntityUid uid, ImperialAtmosShieldGenComponent gen, TransformComponent xform)
    {
        var active = xform.Anchored && xform.GridUid is { } gridUid && _power.IsPowered(uid);
        EntityUid? partner = null;
        var tiles = new List<Vector2i>();

        if (active && TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            var dir = xform.LocalRotation.GetCardinalDir().ToIntVec();
            var start = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            for (var i = 0; i <= gen.MaxRange + 1 && partner == null; i++)
            {
                var tile = start + dir * i;
                tiles.Add(tile);
                if (i == 0)
                    continue;

                foreach (var other in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
                {
                    if (other == uid || !HasComp<ImperialAtmosShieldGenComponent>(other))
                        continue;

                    var otherDir = Transform(other).LocalRotation.GetCardinalDir().ToIntVec();
                    if (otherDir == -dir && _power.IsPowered(other))
                        partner = other;
                }
            }

            // Поле держит тот, у кого меньше EntityUid, чтобы пара не дублировала его.
            if (partner != null && partner.Value.Id < uid.Id)
                partner = null;
        }

        if (partner == null)
        {
            ClearFields(gen);
            return;
        }

        if (gen.Fields.Count == tiles.Count && gen.Fields.All(Exists))
            return;

        ClearFields(gen);
        var gridEnt = xform.GridUid!.Value;
        var gridComp = Comp<MapGridComponent>(gridEnt);
        foreach (var tile in tiles)
            gen.Fields.Add(Spawn(gen.Field, _map.GridTileToLocal(gridEnt, gridComp, tile)));
    }

    private void ClearFields(ImperialAtmosShieldGenComponent gen)
    {
        foreach (var field in gen.Fields)
            QueueDel(field);
        gen.Fields.Clear();
    }

    private void OnShieldShutdown(Entity<ImperialAtmosShieldGenComponent> ent, ref ComponentShutdown args)
    {
        ClearFields(ent.Comp);
    }

    #endregion

    #region Лучи дисков эмиттера

    private void OnEmitterBeamHit(Entity<ImperialEmitterBeamEffectsComponent> ent, ref ProjectileHitEvent args)
    {
        var comp = ent.Comp;
        var target = args.Target;

        if (comp.FireStacks > 0 && TryComp<FlammableComponent>(target, out var flammable))
        {
            _flammable.AdjustFireStacks(target, comp.FireStacks, flammable);
            _flammable.Ignite(target, ent, flammable);
        }

        if (comp.Sanity != 0 && TryComp<SanityComponent>(target, out var sanity))
        {
            _sanity.ModifySanity((target, sanity), comp.Sanity);
            _popup.PopupEntity(Loc.GetString("imperial-emitter-psy"), target, target);
        }

        if (comp.VortexRange > 0)
        {
            // goonchem_vortex: притягивает незакреплённые предметы к точке попадания.
            var center = _transform.GetMapCoordinates(target);
            foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(center, comp.VortexRange))
            {
                if (!TryComp<PhysicsComponent>(item, out var physics) || Transform(item).Anchored)
                    continue;

                var dir = center.Position - _transform.GetMapCoordinates(item).Position;
                if (dir.LengthSquared() < 0.01f)
                    continue;

                _physics.ApplyLinearImpulse(item, Vector2.Normalize(dir) * physics.Mass * 5f, body: physics);
            }
        }
    }

    #endregion
}
