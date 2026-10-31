using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Кабина танка — отдельная карта, как бункер ПВО. Люк снаружи ведёт внутрь (если машина не заперта),
/// в кабине кресла водителя и наводчика (пристегнулся — камера уходит наружу, на машину), казённик основного
/// орудия, боеукладка, десантные скамьи, вокс и ключ. Ключом запирают люк снаружи и изнутри.
/// </summary>
public sealed partial class CadiaVehicleSystem
{
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSub = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly Content.Shared.Movement.Systems.SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg");
    private static readonly SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");
    private static readonly SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");
    private static readonly SoundSpecifier LoadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");
    private static readonly SoundSpecifier ShellOutSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagOut/batrifle_magout.ogg");

    private void InitializeInterior()
    {
        SubscribeLocalEvent<CadiaVehicleSeatComponent, StrappedEvent>(OnSeatStrapped);
        SubscribeLocalEvent<CadiaVehicleSeatComponent, UnstrappedEvent>(OnSeatUnstrapped);
        SubscribeLocalEvent<CadiaVehicleHatchComponent, ActivateInWorldEvent>(OnHatchActivate);
        SubscribeLocalEvent<CadiaVehicleHatchComponent, InteractUsingEvent>(OnHatchInteractUsing);
        SubscribeLocalEvent<CadiaAmmoRackComponent, ActivateInWorldEvent>(OnRackActivate);
        SubscribeLocalEvent<CadiaAmmoRackComponent, InteractUsingEvent>(OnRackInteractUsing);
        SubscribeLocalEvent<CadiaVehicleBreechComponent, InteractUsingEvent>(OnBreechInteractUsing);
        SubscribeLocalEvent<CadiaVehicleBreechComponent, CadiaBreechLoadDoAfterEvent>(OnBreechLoaded);
    }

    // ═══════════════════════ Вход в кабину ═══════════════════════

    private void EnterCabin(Entity<CadiaVehicleComponent> ent, EntityUid user)
    {
        if (ent.Comp.Locked)
        {
            DenyLocked(ent, user);
            return;
        }
        var cabin = EnsureInterior(ent);
        var half = ent.Comp.InteriorSize / 2;
        _audio.PlayPvs(ent.Comp.EnterSound, ent);
        _xform.SetCoordinates(user, new EntityCoordinates(cabin, new Vector2(0.5f, -half.Y + 0.5f)));
        _xform.AttachToGridOrMap(user);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-in-cabin", ("vehicle", Name(ent))), user, user);
    }

    private void DenyLocked(Entity<CadiaVehicleComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-locked"), ent, user, PopupType.SmallCaution);
        _audio.PlayEntity(DenySound, Filter.Entities(user), ent, false);
    }

    /// <summary>Из кабины наружу при гибели машины: отстегнуть, освободить место, поставить рядом с корпусом.</summary>
    private void EvacuateCabinOccupant(Entity<CadiaVehicleComponent> ent, EntityUid occupant)
    {
        _buckle.Unbuckle(occupant, null);
        if (TryComp<CadiaVehicleCrewComponent>(occupant, out var crew))
            LeaveSeat(occupant, crew);
        ExitCabin(ent, occupant);
    }

    private void ExitCabin(EntityUid vehicle, EntityUid user)
    {
        var xform = Transform(vehicle);
        var back = _xform.GetWorldRotation(xform).RotateVec(new Vector2(0, -2f));
        _xform.SetMapCoordinates(user, _xform.GetMapCoordinates(xform).Offset(back + _random.NextVector2(0.3f)));
        _xform.AttachToGridOrMap(user);
        if (TryComp<CadiaVehicleComponent>(vehicle, out var comp))
            _audio.PlayPvs(comp.EnterSound, vehicle);
    }

    /// <summary>
    /// Кабина: пол, стены, люк в корме, кресла водителя и наводчика у носа, казённик и боеукладка (если орудие
    /// заряжается вручную), десантные скамьи по бортам, вокс, лампа и ключ на полу у люка.
    /// </summary>
    private EntityUid EnsureInterior(Entity<CadiaVehicleComponent> ent)
    {
        if (ent.Comp.InteriorMap is { } existing && Exists(existing))
            return existing;

        var map = _map.CreateMap(out var mapId, false);
        _meta.SetEntityName(map, Loc.GetString("cadia-vehicle-cabin-name", ("vehicle", Name(ent))));
        EnsureComp<CadiaVehicleInteriorComponent>(map).Vehicle = ent;

        var grid = EnsureComp<MapGridComponent>(map);
        var gravity = EnsureComp<GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(map, gravity);
        var light = EnsureComp<MapLightComponent>(map);
        light.AmbientLightColor = Color.FromHex("#3a4236");
        Dirty(map, light);
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        _atmos.SetMapAtmosphere(map, false, new GasMixture(moles, Atmospherics.T20C));

        var hx = ent.Comp.InteriorSize.X / 2;
        var hy = ent.Comp.InteriorSize.Y / 2;
        var floor = new Tile(_tiles["FloorDark"].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -hx - 1; x <= hx + 1; x++)
        {
            for (var y = -hy - 1; y <= hy + 1; y++)
                tiles.Add((new Vector2i(x, y), floor));
        }
        _map.SetTiles(map, grid, tiles);

        EntityUid At(string proto, int x, int y) => Spawn(proto, new EntityCoordinates(map, new Vector2(x + 0.5f, y + 0.5f)));

        for (var x = -hx - 1; x <= hx + 1; x++)
        {
            for (var y = -hy - 1; y <= hy + 1; y++)
            {
                if (Math.Abs(x) != hx + 1 && Math.Abs(y) != hy + 1)
                    continue;
                if (x == 0 && y == -hy - 1)
                    continue;
                At("CadiaWallReinforced", x, y);
            }
        }

        EnsureComp<CadiaVehicleHatchComponent>(At("CadiaVehicleHatch", 0, -hy - 1)).Vehicle = ent;
        if (ent.Comp.HasDriverSeat)
            EnsureComp<CadiaVehicleSeatComponent>(At("CadiaVehicleDriverSeat", -1, hy)).Vehicle = ent;
        if (ent.Comp.HasGunnerSeat)
            EnsureComp<CadiaVehicleSeatComponent>(At("CadiaVehicleGunnerSeat", 1, hy)).Vehicle = ent;

        // Казённик между креслами, боеукладка у борта — заряжающим может быть и наводчик, и десант.
        if (ent.Comp.Weapons.Any(w => w.Breech))
        {
            var breech = At("CadiaVehicleBreech", 0, hy);
            EnsureComp<CadiaVehicleBreechComponent>(breech).Vehicle = ent;
            // Боеукладка в кормовом углу: у кресел остаётся проход, наводчик не оказывается в ловушке.
            var rack = At("CadiaAmmoRack", hx, -hy);
            EnsureComp<CadiaAmmoRackComponent>(rack).Vehicle = ent;
            UpdateBreechVisuals(ent);
        }

        // Десантные скамьи по бортам, от носа к корме.
        var placed = 0;
        for (var y = hy - 1; y > -hy - 1 && placed < ent.Comp.Passengers; y--)
        {
            foreach (var x in new[] { -hx, hx })
            {
                if (placed >= ent.Comp.Passengers)
                    break;
                EnsureComp<CadiaVehicleSeatComponent>(At("CadiaVehicleBench", x, y)).Vehicle = ent;
                placed++;
            }
        }

        At(ent.Comp.InteriorSpeaker, -hx - 1, 0);
        At("CadiaBunkerLamp", hx + 1, hy);
        EnsureComp<CadiaVehicleKeyComponent>(At("CadiaVehicleKey", 0, -hy)).Vehicle = ent;

        _map.InitializeMap(mapId);
        ent.Comp.InteriorMap = map;
        return map;
    }

    /// <summary>
    /// Стрелок открытой позиции смотрит туда же, куда ствол. Позицию трогать нельзя: ремень отстёгивает при
    /// любом сдвиге от точки сиденья, поэтому за ствол его смещает клиент, только спрайтом.
    /// </summary>
    private void PlaceGunner(Entity<CadiaVehicleComponent> ent, EntityUid gunner)
    {
        _xform.SetLocalRotation(gunner, FireAngle(ent) + Math.PI);
    }

    /// <summary>Все, кто сейчас в кабине.</summary>
    private List<EntityUid> CabinOccupants(Entity<CadiaVehicleComponent> ent)
    {
        var result = new List<EntityUid>();
        if (ent.Comp.InteriorMap is not { } cabin || !Exists(cabin))
            return result;
        var mobs = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == cabin)
                result.Add(uid);
        }
        return result;
    }

    // ═══════════════════════ Кресла ═══════════════════════

    private void OnSeatStrapped(Entity<CadiaVehicleSeatComponent> seat, ref StrappedEvent args)
    {
        if (seat.Comp.Vehicle is not { } vehicleUid || !TryComp<CadiaVehicleComponent>(vehicleUid, out var vehicle))
            return;
        var user = args.Buckle.Owner;
        var ent = new Entity<CadiaVehicleComponent>(vehicleUid, vehicle);
        if (TryComp<CadiaVehicleCrewComponent>(user, out var old) && old.Vehicle == vehicleUid)
            return;

        var crew = EnsureComp<CadiaVehicleCrewComponent>(user);
        crew.Vehicle = vehicleUid;
        crew.Seat = seat.Comp.Seat;
        Dirty(user, crew);

        switch (seat.Comp.Seat)
        {
            case CadiaVehicleSeat.Driver:
                vehicle.Driver = user;
                _audio.PlayPvs(vehicle.EngineStartSound, vehicleUid);
                break;
            case CadiaVehicleSeat.Gunner:
                vehicle.Gunner = user;
                break;
            default:
                vehicle.PassengerCount++;
                break;
        }
        Dirty(vehicleUid, vehicle);

        // Водитель и наводчик смотрят наружу — камера на машине.
        if (seat.Comp.Seat != CadiaVehicleSeat.Passenger)
        {
            _eye.SetTarget(user, vehicleUid);
            if ((seat.Comp.Seat == CadiaVehicleSeat.Gunner || vehicle.DriverIsGunner) && vehicle.GunnerZoom > 1f)
                _contentEye.SetZoom(user, new Vector2(vehicle.GunnerZoom), ignoreLimits: true);
            if (TryComp<ActorComponent>(user, out var actor))
                _viewSub.AddViewSubscriber(vehicleUid, actor.PlayerSession);
        }

        GrantActions(ent, user, crew, cabin: true);
        var entered = vehicle.GunPost ? "cadia-vehicle-entered-gunpost" : $"cadia-vehicle-entered-{seat.Comp.Seat.ToString().ToLowerInvariant()}";
        _popup.PopupEntity(Loc.GetString(entered, ("vehicle", Name(vehicleUid))), user, user, PopupType.Medium);
    }

    private void OnSeatUnstrapped(Entity<CadiaVehicleSeatComponent> seat, ref UnstrappedEvent args)
    {
        var user = args.Buckle.Owner;
        if (!TryComp<CadiaVehicleCrewComponent>(user, out var crew) || seat.Comp.Vehicle is not { } vehicleUid || crew.Vehicle != vehicleUid)
            return;
        LeaveSeat(user, crew);
    }

    /// <summary>Встал с кресла (или кабину разнесло): место свободно, камера вернулась, действия сняты.</summary>
    private void LeaveSeat(EntityUid user, CadiaVehicleCrewComponent crew)
    {
        if (TryComp<CadiaVehicleComponent>(crew.Vehicle, out var vehicle))
        {
            // С открытой позиции боец отходит за орудие, иначе встал бы внутрь его корпуса.
            if (vehicle.GunPost && !TerminatingOrDeleted(crew.Vehicle))
            {
                var back = FireAngle((crew.Vehicle, vehicle)).RotateVec(new Vector2(0, -1.1f));
                _xform.SetMapCoordinates(user, _xform.GetMapCoordinates(crew.Vehicle).Offset(back));
                _xform.AttachToGridOrMap(user);
            }
            if (vehicle.Driver == user)
            {
                vehicle.Driver = null;
                if (TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(crew.Vehicle, out var body))
                    _physics.SetLinearVelocity(crew.Vehicle, Vector2.Zero, body: body);
            }
            else if (vehicle.Gunner == user)
            {
                vehicle.Gunner = null;
                vehicle.Firing = false;
            }
            else if (crew.Seat == CadiaVehicleSeat.Passenger)
            {
                vehicle.PassengerCount = Math.Max(0, vehicle.PassengerCount - 1);
            }
            Dirty(crew.Vehicle, vehicle);
        }

        if (crew.Seat != CadiaVehicleSeat.Passenger)
        {
            _eye.SetTarget(user, null);
            if (crew.Seat == CadiaVehicleSeat.Gunner || CompOrNull<CadiaVehicleComponent>(crew.Vehicle)?.GunPost == true)
                _contentEye.ResetZoom(user);
            if (TryComp<ActorComponent>(user, out var actor) && Exists(crew.Vehicle))
                _viewSub.RemoveViewSubscriber(crew.Vehicle, actor.PlayerSession);
        }
        foreach (var action in crew.Actions)
            _actions.RemoveAction(user, action);
        RemComp<CadiaVehicleCrewComponent>(user);
    }

    // ═══════════════════════ Люк и ключ ═══════════════════════

    private void OnHatchActivate(Entity<CadiaVehicleHatchComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        if (ent.Comp.Vehicle is not { } vehicle || TerminatingOrDeleted(vehicle))
            return;
        // Запертый люк не выпускает и изнутри: сначала отпереть ключом.
        if (TryComp<CadiaVehicleComponent>(vehicle, out var comp) && comp.Locked)
        {
            DenyLocked((vehicle, comp), args.User);
            return;
        }
        ExitCabin(vehicle, args.User);
    }

    private void OnHatchInteractUsing(Entity<CadiaVehicleHatchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Vehicle is not { } vehicle || !TryComp<CadiaVehicleComponent>(vehicle, out var comp))
            return;
        args.Handled = TryToggleLock((vehicle, comp), args.Used, args.User) || TryLoadBelt((vehicle, comp), args.Used, args.User);
    }

    /// <summary>Ящик боеприпасов пополняет автоматическое оружие; основное орудие кормится только через казённик.</summary>
    private bool TryLoadBelt(Entity<CadiaVehicleComponent> ent, EntityUid used, EntityUid user)
    {
        if (!TryComp<CadiaFlakAmmoComponent>(used, out var belt))
            return false;
        for (var i = 0; i < ent.Comp.Weapons.Count; i++)
        {
            if (!ent.Comp.Weapons[i].Breech)
                ent.Comp.Ammo[i] = Math.Min(ent.Comp.Weapons[i].MaxAmmo, ent.Comp.Ammo[i] + belt.Rounds);
        }
        Dirty(ent);
        QueueDel(used);
        _audio.PlayPvs(LoadSound, user);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-belt-loaded"), user, user);
        return true;
    }

    /// <summary>Ключ этой машины запирает и отпирает люк.</summary>
    private bool TryToggleLock(Entity<CadiaVehicleComponent> ent, EntityUid used, EntityUid user)
    {
        if (!TryComp<CadiaVehicleKeyComponent>(used, out var key))
            return false;
        if (key.Vehicle != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-key-wrong"), user, user);
            return true;
        }
        ent.Comp.Locked = !ent.Comp.Locked;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.Locked ? LockSound : UnlockSound, user);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Locked ? "cadia-vehicle-key-locked" : "cadia-vehicle-key-unlocked", ("vehicle", Name(ent))), user, user);
        return true;
    }

    // ═══════════════════════ Боеукладка и казённик ═══════════════════════

    private void OnRackActivate(Entity<CadiaAmmoRackComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || ent.Comp.Vehicle is not { } vehicle || !TryComp<CadiaVehicleComponent>(vehicle, out var comp))
            return;
        args.Handled = true;
        if (comp.RackCount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-rack-empty"), ent, args.User);
            return;
        }
        var shell = Spawn("CadiaTankShell", Transform(args.User).Coordinates);
        if (!_hands.TryPickupAnyHand(args.User, shell))
        {
            QueueDel(shell);
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-hands-full"), ent, args.User);
            return;
        }
        comp.RackCount--;
        Dirty(vehicle, comp);
        _audio.PlayPvs(ShellOutSound, ent);
        UpdateBreechVisuals((vehicle, comp));
    }

    private void OnRackInteractUsing(Entity<CadiaAmmoRackComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<CadiaTankShellComponent>(args.Used) || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<CadiaVehicleComponent>(vehicle, out var comp))
            return;
        args.Handled = true;
        if (comp.RackCount >= comp.RackShells)
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-rack-full"), ent, args.User);
            return;
        }
        QueueDel(args.Used);
        comp.RackCount++;
        Dirty(vehicle, comp);
        _audio.PlayPvs(LoadSound, ent);
        UpdateBreechVisuals((vehicle, comp));
    }

    private void OnBreechInteractUsing(Entity<CadiaVehicleBreechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Vehicle is not { } vehicle || !TryComp<CadiaVehicleComponent>(vehicle, out var comp))
            return;

        if (TryLoadBelt((vehicle, comp), args.Used, args.User))
        {
            args.Handled = true;
            return;
        }

        if (!HasComp<CadiaTankShellComponent>(args.Used))
            return;
        args.Handled = true;
        if (comp.Loaded)
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-breech-full"), ent, args.User);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(1.5), new CadiaBreechLoadDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBreechLoaded(Entity<CadiaVehicleBreechComponent> ent, ref CadiaBreechLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } shell || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<CadiaVehicleComponent>(vehicle, out var comp) || comp.Loaded)
            return;
        args.Handled = true;
        QueueDel(shell);
        comp.Loaded = true;
        Dirty(vehicle, comp);
        _audio.PlayPvs(LoadSound, ent, AudioParams.Default.WithVolume(4));
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-breech-loaded"), ent, PopupType.Medium);
        if (comp.Gunner is { } gunner)
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-breech-loaded-gunner"), gunner, gunner, PopupType.Medium);
        UpdateBreechVisuals((vehicle, comp));
    }

    /// <summary>Казённик показывает снаряд, боеукладка — латунь гильз, пока они есть.</summary>
    private void UpdateBreechVisuals(Entity<CadiaVehicleComponent> ent)
    {
        var breeches = EntityQueryEnumerator<CadiaVehicleBreechComponent>();
        while (breeches.MoveNext(out var uid, out var breech))
        {
            if (breech.Vehicle == ent.Owner)
                _appearance.SetData(uid, CadiaVehicleVisuals.Loaded, ent.Comp.Loaded);
        }
        var racks = EntityQueryEnumerator<CadiaAmmoRackComponent>();
        while (racks.MoveNext(out var uid, out var rack))
        {
            if (rack.Vehicle == ent.Owner)
                _appearance.SetData(uid, CadiaVehicleVisuals.RackFull, ent.Comp.RackCount > 0);
        }
    }
}
