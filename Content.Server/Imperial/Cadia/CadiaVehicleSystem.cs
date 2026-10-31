using System.Linq;
using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Техника Кадии: Леман Расс, Химера, Часовой. Танки — с кабиной (отдельная карта, см. Interior), Часовой — экипаж
/// в корпусе (контейнеры мест). Водитель ведёт
/// машину кнопками движения, наводчик ведёт башню за курсором и стреляет. Дымовые гранатомёты, ремонт сваркой
/// и ритуалом техножреца, подбитая машина взрывается и оставляет остов, экипаж выбрасывает наружу.
/// </summary>
public sealed partial class CadiaVehicleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly Content.Shared.Stunnable.SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    private static readonly SoundSpecifier RitualSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");
    private static readonly SoundSpecifier RitualDoneSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
    private static readonly SoundSpecifier EmptySound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");

    private const float AlignedFire = 3f;
    private const float RunOverMinSpeed = 1f;
    private const float RunOverTankDamage = 20f;
    private const float RunOverWalkerDamage = 8f;
    private static readonly TimeSpan RunOverCooldown = TimeSpan.FromSeconds(1.5);
    private static readonly SoundSpecifier RunOverSound = new SoundPathSpecifier("/Audio/Effects/bodyfall2.ogg");
    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> BluntDamage = "Blunt";
    private readonly Dictionary<EntityUid, TimeSpan> _nextRunOver = new();
    private static readonly TimeSpan DirtyRate = TimeSpan.FromSeconds(0.1);
    private TimeSpan _nextDirty;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CadiaVehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CadiaVehicleComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<CadiaVehicleComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CadiaVehicleComponent, CadiaVehicleEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<CadiaVehicleComponent, CadiaVehicleExitActionEvent>(OnExitAction);
        SubscribeLocalEvent<CadiaVehicleComponent, CadiaVehicleSwitchActionEvent>(OnSwitchAction);
        SubscribeLocalEvent<CadiaVehicleComponent, CadiaVehicleSmokeActionEvent>(OnSmokeAction);
        SubscribeLocalEvent<CadiaVehicleComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<CadiaVehicleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CadiaVehicleComponent, CadiaVehicleRitualDoAfterEvent>(OnRitual);
        SubscribeLocalEvent<CadiaVehicleComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CadiaVehicleComponent, EntRemovedFromContainerMessage>(OnCrewRemoved);

        SubscribeLocalEvent<CadiaVehicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CadiaVehicleComponent, StartCollideEvent>(OnRunOver);

        SubscribeNetworkEvent<CadiaVehicleAimEvent>(OnAim);
        SubscribeNetworkEvent<CadiaVehicleFireEvent>(OnFire);

        InitializeInterior();
    }

    private void OnMapInit(Entity<CadiaVehicleComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, CadiaVehicleComponent.DriverSlot);
        if (ent.Comp.HasGunnerSeat)
            _container.EnsureContainer<ContainerSlot>(ent, CadiaVehicleComponent.GunnerSlot);
        if (ent.Comp.Passengers > 0)
            _container.EnsureContainer<Container>(ent, CadiaVehicleComponent.PassengerContainer);

        ent.Comp.Ammo = ent.Comp.Weapons.Select(w => w.Ammo).ToList();
        ent.Comp.NextShot = ent.Comp.Weapons.Select(_ => TimeSpan.Zero).ToList();
        ent.Comp.TurretAngle = _xform.GetWorldRotation(ent);
        ent.Comp.TurretTarget = ent.Comp.TurretAngle;
        ent.Comp.SmokeCharges = ent.Comp.MaxSmokeCharges;
        ent.Comp.RackCount = ent.Comp.RackShells;
        // Открытая позиция — сама себе кресло: боец пристёгивается к ней и остаётся виден.
        if (ent.Comp.GunPost)
        {
            var seat = EnsureComp<CadiaVehicleSeatComponent>(ent);
            seat.Vehicle = ent;
            seat.Seat = CadiaVehicleSeat.Driver;
            Dirty(ent, seat);
        }
        Dirty(ent);
    }

    // ═══════════════════════ Посадка ═══════════════════════

    private bool SeatFree(Entity<CadiaVehicleComponent> ent, CadiaVehicleSeat seat)
    {
        return seat switch
        {
            CadiaVehicleSeat.Driver => ent.Comp.Driver == null,
            CadiaVehicleSeat.Gunner => ent.Comp.HasGunnerSeat && ent.Comp.Gunner == null,
            _ => ent.Comp.PassengerCount < ent.Comp.Passengers,
        };
    }

    private void OnVerbs(Entity<CadiaVehicleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<CadiaVehicleCrewComponent>(args.User))
            return;
        var user = args.User;
        if (ent.Comp.GunPost)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-vehicle-verb-gunpost"),
                Priority = 5,
                Act = () => _buckle.TryBuckle(user, user, ent),
            });
            return;
        }
        if (ent.Comp.Interior)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-vehicle-verb-hatch"),
                Priority = 5,
                Act = () => StartEnter(ent, user, CadiaVehicleSeat.Passenger),
            });
            return;
        }
        foreach (var seat in Enum.GetValues<CadiaVehicleSeat>())
        {
            if (!SeatFree(ent, seat))
                continue;
            var s = seat;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(ent.Comp.GunPost ? "cadia-vehicle-verb-gunpost" : $"cadia-vehicle-verb-{seat.ToString().ToLowerInvariant()}"),
                Priority = 5 - (int) seat,
                Act = () => StartEnter(ent, user, s),
            });
        }
    }

    private void OnActivate(Entity<CadiaVehicleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || HasComp<CadiaVehicleCrewComponent>(args.User))
            return;
        if (ent.Comp.GunPost)
        {
            args.Handled = true;
            _buckle.TryBuckle(args.User, args.User, ent);
            return;
        }
        if (ent.Comp.Interior)
        {
            args.Handled = true;
            StartEnter(ent, args.User, CadiaVehicleSeat.Passenger);
            return;
        }
        foreach (var seat in Enum.GetValues<CadiaVehicleSeat>())
        {
            if (!SeatFree(ent, seat))
                continue;
            args.Handled = true;
            StartEnter(ent, args.User, seat);
            return;
        }
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-full"), ent, args.User);
    }

    private void StartEnter(Entity<CadiaVehicleComponent> ent, EntityUid user, CadiaVehicleSeat seat)
    {
        if (ent.Comp.Interior && ent.Comp.Locked)
        {
            DenyLocked(ent, user);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(1.5), new CadiaVehicleEnterDoAfterEvent(seat), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPvs(ent.Comp.EnterSound, ent);
    }

    private void OnEnterDoAfter(Entity<CadiaVehicleComponent> ent, ref CadiaVehicleEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        var user = args.User;
        if (ent.Comp.Interior)
        {
            EnterCabin(ent, user);
            return;
        }
        if (!SeatFree(ent, args.Seat) || HasComp<CadiaVehicleCrewComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-seat-taken"), ent, user);
            return;
        }

        var containerId = args.Seat switch
        {
            CadiaVehicleSeat.Driver => CadiaVehicleComponent.DriverSlot,
            CadiaVehicleSeat.Gunner => CadiaVehicleComponent.GunnerSlot,
            _ => CadiaVehicleComponent.PassengerContainer,
        };
        if (!_container.TryGetContainer(ent, containerId, out var container) || !_container.Insert(user, container))
            return;

        var crew = EnsureComp<CadiaVehicleCrewComponent>(user);
        crew.Vehicle = ent;
        crew.Seat = args.Seat;
        Dirty(user, crew);

        switch (args.Seat)
        {
            case CadiaVehicleSeat.Driver:
                ent.Comp.Driver = user;
                _audio.PlayPvs(ent.Comp.EngineStartSound, ent);
                break;
            case CadiaVehicleSeat.Gunner:
                ent.Comp.Gunner = user;
                break;
            default:
                ent.Comp.PassengerCount++;
                break;
        }
        Dirty(ent);

        GrantActions(ent, user, crew);
        var entered = ent.Comp.GunPost ? "cadia-vehicle-entered-gunpost" : $"cadia-vehicle-entered-{args.Seat.ToString().ToLowerInvariant()}";
        _popup.PopupEntity(Loc.GetString(entered, ("vehicle", Name(ent))), user, user, PopupType.Medium);
    }

    private void GrantActions(Entity<CadiaVehicleComponent> ent, EntityUid user, CadiaVehicleCrewComponent crew, bool cabin = false)
    {
        void Add(EntProtoId proto)
        {
            if (_actions.AddAction(user, proto, ent) is { } action)
                crew.Actions.Add(action);
        }

        // На десантной скамье действий нет: встают обычным отстёгиванием.
        if (!cabin || crew.Seat != CadiaVehicleSeat.Passenger)
            Add(ent.Comp.ExitAction);
        var gunner = crew.Seat == CadiaVehicleSeat.Gunner || (crew.Seat == CadiaVehicleSeat.Driver && ent.Comp.DriverIsGunner);
        if (gunner && ent.Comp.Weapons.Count > 1)
            Add(ent.Comp.SwitchAction);
        if (crew.Seat == CadiaVehicleSeat.Driver && ent.Comp.MaxSmokeCharges > 0)
            Add(ent.Comp.SmokeAction);
    }

    private void OnExitAction(Entity<CadiaVehicleComponent> ent, ref CadiaVehicleExitActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        if (ent.Comp.Interior || ent.Comp.GunPost)
            _buckle.TryUnbuckle(args.Performer, args.Performer, popup: false);
        else
            Eject(ent, args.Performer);
    }

    /// <summary>Выход: позади машины, на свободную клетку.</summary>
    public void Eject(Entity<CadiaVehicleComponent> ent, EntityUid user)
    {
        if (!_container.TryGetContainingContainer((user, null, null), out var container) || container.Owner != ent.Owner)
            return;
        _container.Remove(user, container);
        var xform = Transform(ent);
        var back = _xform.GetWorldRotation(xform).RotateVec(new Vector2(0, -1.8f));
        var target = _xform.GetMapCoordinates(xform).Offset(back + _random.NextVector2(0.4f));
        _xform.SetMapCoordinates(user, target);
        _xform.AttachToGridOrMap(user);
        _audio.PlayPvs(ent.Comp.EnterSound, ent);
    }

    /// <summary>Кто бы ни вышел из машины (действие, взрыв, удаление) — место освобождается, действия снимаются.</summary>
    private void OnCrewRemoved(Entity<CadiaVehicleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        var user = args.Entity;
        if (ent.Comp.Driver == user)
        {
            ent.Comp.Driver = null;
            if (TryComp<PhysicsComponent>(ent, out var body))
                _physics.SetLinearVelocity(ent, Vector2.Zero, body: body);
        }
        else if (ent.Comp.Gunner == user)
        {
            ent.Comp.Gunner = null;
            ent.Comp.Firing = false;
        }
        else if (args.Container.ID == CadiaVehicleComponent.PassengerContainer)
        {
            ent.Comp.PassengerCount = Math.Max(0, ent.Comp.PassengerCount - 1);
        }
        Dirty(ent);

        if (TryComp<CadiaVehicleCrewComponent>(user, out var crew))
        {
            foreach (var action in crew.Actions)
                _actions.RemoveAction(user, action);
            RemComp<CadiaVehicleCrewComponent>(user);
        }
    }

    // ═══════════════════════ Наводка и огонь ═══════════════════════

    private bool IsGunner(Entity<CadiaVehicleComponent> ent, EntityUid user)
    {
        return ent.Comp.Gunner == user || (ent.Comp.DriverIsGunner && ent.Comp.Driver == user);
    }

    private void OnAim(CadiaVehicleAimEvent ev, EntitySessionEventArgs args)
    {
        var vehicle = GetEntity(ev.Vehicle);
        if (args.SenderSession.AttachedEntity is not { } user || !TryComp<CadiaVehicleComponent>(vehicle, out var comp) || !IsGunner((vehicle, comp), user))
            return;
        var target = _xform.ToMapCoordinates(GetCoordinates(ev.Target));
        var origin = _xform.GetMapCoordinates(vehicle);
        if (target.MapId != origin.MapId)
            return;
        var delta = target.Position - origin.Position;
        if (delta.LengthSquared() < 0.05f)
            return;
        comp.TurretTarget = new Angle(MathF.Atan2(-delta.X, delta.Y));
        comp.AimPoint = target;
    }

    private void OnFire(CadiaVehicleFireEvent ev, EntitySessionEventArgs args)
    {
        var vehicle = GetEntity(ev.Vehicle);
        if (args.SenderSession.AttachedEntity is not { } user || !TryComp<CadiaVehicleComponent>(vehicle, out var comp) || !IsGunner((vehicle, comp), user))
            return;
        if (ev.Down && _veil.Blocks(user, predicted: false))
            return;
        comp.Firing = ev.Down;
        Dirty(vehicle, comp);
        // Пушка стреляет по нажатию, пулемёт — пока кнопка зажата.
        if (ev.Down)
            TryFire((vehicle, comp), user);
    }

    private void OnSwitchAction(Entity<CadiaVehicleComponent> ent, ref CadiaVehicleSwitchActionEvent args)
    {
        if (args.Handled || ent.Comp.Weapons.Count == 0)
            return;
        args.Handled = true;
        ent.Comp.SelectedWeapon = (ent.Comp.SelectedWeapon + 1) % ent.Comp.Weapons.Count;
        Dirty(ent);
        _audio.PlayEntity(new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg"), Filter.Entities(args.Performer), ent, false);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-weapon-selected", ("weapon", Loc.GetString(ent.Comp.Weapons[ent.Comp.SelectedWeapon].Name))), args.Performer, args.Performer);
    }

    /// <summary>
    /// Куда смотрит нос (0 — север). Спрайт шагохода выбирает направление как моб: у него угол 0 — это юг,
    /// поэтому корпус шагохода повёрнут на 180° относительно танка.
    /// </summary>
    private Angle HullFacing(Entity<CadiaVehicleComponent> ent)
    {
        var rotation = _xform.GetWorldRotation(ent);
        return ent.Comp.TankControls ? rotation : rotation + Math.PI;
    }

    /// <summary>Куда смотрит ствол: башня или нос шагохода.</summary>
    private Angle FireAngle(Entity<CadiaVehicleComponent> ent)
    {
        if (ent.Comp.HasTurret)
            return ent.Comp.TurretAngle;
        // Шагоход: в сторону прицела, но в пределах сектора от носа.
        var hull = HullFacing(ent);
        var diff = Angle.ShortestDistance(hull, ent.Comp.TurretTarget).Degrees;
        diff = Math.Clamp(diff, -ent.Comp.FireArc, ent.Comp.FireArc);
        return hull + Angle.FromDegrees(diff);
    }

    private void TryFire(Entity<CadiaVehicleComponent> ent, EntityUid gunner)
    {
        var index = ent.Comp.SelectedWeapon;
        if (index < 0 || index >= ent.Comp.Weapons.Count)
            return;
        var weapon = ent.Comp.Weapons[index];
        var now = _timing.CurTime;
        if (now < ent.Comp.NextShot[index])
            return;
        if (weapon.Breech && !ent.Comp.Loaded)
        {
            ent.Comp.NextShot[index] = now + TimeSpan.FromSeconds(0.6);
            _audio.PlayEntity(EmptySound, Filter.Entities(gunner), ent, false);
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-breech-empty"), gunner, gunner, PopupType.SmallCaution);
            return;
        }
        if (!weapon.Breech && ent.Comp.Ammo[index] <= 0)
        {
            ent.Comp.NextShot[index] = now + TimeSpan.FromSeconds(0.6);
            _audio.PlayEntity(EmptySound, Filter.Entities(gunner), ent, false);
            return;
        }

        if (weapon.Breech)
        {
            ent.Comp.Loaded = false;
            UpdateBreechVisuals(ent);
        }
        else
        {
            ent.Comp.Ammo[index]--;
        }
        ent.Comp.NextShot[index] = now + weapon.Cooldown;
        Dirty(ent);

        if (weapon.Indirect)
        {
            FireIndirect(ent, weapon);
            return;
        }

        var angle = FireAngle(ent) + Angle.FromDegrees(_random.NextFloat(-weapon.Spread, weapon.Spread));
        var dir = angle.RotateVec(new Vector2(0, 1));
        var origin = _xform.GetMapCoordinates(ent);
        var muzzle = origin.Offset(dir * ent.Comp.MuzzleOffset);
        // Шагоход: пушка на руке — дуло по направлению спрайта, выстрел идёт оттуда.
        if (!ent.Comp.HasTurret && ent.Comp.Muzzles.TryGetValue(_xform.GetWorldRotation(ent).GetCardinalDir(), out var arm))
            muzzle = origin.Offset(arm + dir * 0.4f);

        var projectile = Spawn(weapon.Projectile, muzzle);
        var velocity = TryComp<PhysicsComponent>(ent, out var body) ? body.LinearVelocity : Vector2.Zero;
        _gun.ShootProjectile(projectile, dir, velocity, ent, gunner, weapon.Speed);

        Spawn("CadiaFlakMuzzle", muzzle);
        if (weapon.Sound != null)
            _audio.PlayPvs(weapon.Sound, ent, AudioParams.Default.WithVariation(0.05f));

        if (weapon.Kick > 0)
        {
            foreach (var crew in Crew(ent))
                _recoil.KickCamera(crew, -dir * weapon.Kick);
        }
    }

    /// <summary>Навесной выстрел: метка удара на точке прицела, снаряд падает через несколько секунд с разбросом.</summary>
    private void FireIndirect(Entity<CadiaVehicleComponent> ent, CadiaVehicleWeapon weapon)
    {
        var origin = _xform.GetMapCoordinates(ent);
        var aim = ent.Comp.AimPoint;
        var offset = aim.MapId == origin.MapId ? aim.Position - origin.Position : ent.Comp.TurretAngle.RotateVec(new Vector2(0, weapon.IndirectRange));
        if (offset.Length() > weapon.IndirectRange)
            offset = offset.Normalized() * weapon.IndirectRange;
        var point = origin.Offset(offset + _random.NextVector2(weapon.Spread));

        var now = _timing.CurTime;
        var strike = Spawn("CadiaOrbitalStrikeMarker", point);
        var comp = EnsureComp<CadiaOrbitalStrikeComponent>(strike);
        comp.Kind = CadiaBatteryKind.Macro;
        comp.Start = now;
        comp.Impact = now + TimeSpan.FromSeconds(4);
        comp.Intensity = weapon.IndirectIntensity;
        comp.Slope = 6f;
        comp.MaxTileIntensity = 20f;
        comp.Source = ent;
        Dirty(strike, comp);

        Spawn("CadiaFlakMuzzle", origin.Offset(ent.Comp.TurretAngle.RotateVec(new Vector2(0, ent.Comp.MuzzleOffset))));
        if (weapon.Sound != null)
            _audio.PlayPvs(weapon.Sound, ent, AudioParams.Default.WithVariation(0.05f));
        if (weapon.Kick > 0)
        {
            var back = -ent.Comp.TurretAngle.RotateVec(new Vector2(0, 1));
            foreach (var crew in Crew(ent))
                _recoil.KickCamera(crew, back * weapon.Kick);
        }
    }

    private IEnumerable<EntityUid> Crew(Entity<CadiaVehicleComponent> vehicle)
    {
        if (vehicle.Comp.GunPost)
        {
            if (vehicle.Comp.Driver is { } gunner)
                yield return gunner;
            yield break;
        }
        if (vehicle.Comp.Interior)
        {
            foreach (var entity in CabinOccupants(vehicle))
                yield return entity;
            yield break;
        }
        foreach (var id in new[] { CadiaVehicleComponent.DriverSlot, CadiaVehicleComponent.GunnerSlot, CadiaVehicleComponent.PassengerContainer })
        {
            if (!_container.TryGetContainer(vehicle, id, out var container))
                continue;
            foreach (var entity in container.ContainedEntities.ToList())
                yield return entity;
        }
    }

    // ═══════════════════════ Дым ═══════════════════════

    private void OnSmokeAction(Entity<CadiaVehicleComponent> ent, ref CadiaVehicleSmokeActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var now = _timing.CurTime;
        if (ent.Comp.SmokeCharges <= 0 || now < ent.Comp.NextSmoke)
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-smoke-empty"), args.Performer, args.Performer);
            return;
        }
        ent.Comp.SmokeCharges--;
        ent.Comp.NextSmoke = now + ent.Comp.SmokeCooldown;
        Dirty(ent);

        // Завеса: полукольцо впереди и кольцо вокруг корпуса.
        var origin = _xform.GetMapCoordinates(ent);
        var hull = HullFacing(ent);
        for (var i = 0; i < 12; i++)
        {
            var a = hull + Angle.FromDegrees(i * 30);
            Spawn(ent.Comp.SmokeCloud, origin.Offset(a.RotateVec(new Vector2(0, 2.4f))));
        }
        for (var i = -3; i <= 3; i++)
        {
            var a = hull + Angle.FromDegrees(i * 20);
            Spawn(ent.Comp.SmokeCloud, origin.Offset(a.RotateVec(new Vector2(0, 4f))));
        }
        _audio.PlayPvs(ent.Comp.SmokeSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-smoke-fired", ("left", ent.Comp.SmokeCharges)), args.Performer, args.Performer);
    }

    // ═══════════════════════ Повреждения и ремонт ═══════════════════════

    /// <summary>Наезд: чем быстрее и тяжелее машина, тем страшнее. Танк бьёт сильнее шагохода, стоящая машина не бьёт.</summary>
    private void OnRunOver(Entity<CadiaVehicleComponent> ent, ref StartCollideEvent args)
    {
        var speed = args.OurBody.LinearVelocity.Length();
        if (speed < RunOverMinSpeed || !HasComp<Content.Shared.Mobs.Components.MobStateComponent>(args.OtherEntity) || !_mobState.IsAlive(args.OtherEntity))
            return;
        var now = _timing.CurTime;
        if (now < _nextRunOver.GetValueOrDefault(args.OtherEntity))
            return;
        _nextRunOver[args.OtherEntity] = now + RunOverCooldown;

        var blunt = new DamageSpecifier(_proto.Index(BluntDamage), speed * (ent.Comp.TankControls ? RunOverTankDamage : RunOverWalkerDamage));
        _damage.TryChangeDamage(args.OtherEntity, blunt, origin: ent.Comp.Driver ?? ent.Owner);
        _stun.TryUpdateParalyzeDuration(args.OtherEntity, TimeSpan.FromSeconds(2));
        _audio.PlayPvs(RunOverSound, args.OtherEntity);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-run-over", ("vehicle", Name(ent))), args.OtherEntity, args.OtherEntity, PopupType.LargeCaution);
    }

    /// <summary>
    /// Подбитая машина: остаётся горящий остов-укрытие, экипаж выскакивает обожжённым. Взрыв боекомплекта срабатывает
    /// на следующем кадре, поэтому выпрыгнувшим дают пару секунд защиты, иначе их добивает собственный танк.
    /// </summary>
    private void OnDestroyed(Entity<CadiaVehicleComponent> ent, ref DestructionEventArgs args)
    {
        var coords = _xform.GetMapCoordinates(ent);
        var wreck = Spawn(ent.Comp.Wreck, coords);
        _xform.SetWorldRotation(wreck, _xform.GetWorldRotation(ent));
        Spawn(WreckFire, coords);

        var burn = new DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>(HeatDamage), 30);
        foreach (var crew in Crew(ent).ToList())
        {
            if (ent.Comp.Interior)
                EvacuateCabinOccupant(ent, crew);
            else
                Eject(ent, crew);
            _damage.TryChangeDamage(crew, burn, true);
            _frontline.Protect(crew, CrewEscape);
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-destroyed-crew"), crew, crew, PopupType.LargeCaution);
        }
    }

    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> HeatDamage = "Heat";
    private static readonly EntProtoId WreckFire = "CadiaWreckFire";
    private static readonly TimeSpan CrewEscape = TimeSpan.FromSeconds(2.5);
    [Dependency] private readonly CadiaFrontlineSystem _frontline = default!;

    /// <summary>Машину удалили (подбили или стёрли): все из кабины наружу, кабину убрать.</summary>
    private void OnShutdown(Entity<CadiaVehicleComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.InteriorMap is not { } cabin || TerminatingOrDeleted(cabin))
            return;
        foreach (var crew in CabinOccupants(ent))
            EvacuateCabinOccupant(ent, crew);
        QueueDel(cabin);
        ent.Comp.InteriorMap = null;
    }

    /// <summary>Ритуал техножреца: топор Омниссии на корпусе — долгий молебен, корпус латается.</summary>
    private void OnInteractUsing(Entity<CadiaVehicleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (ent.Comp.Interior && TryToggleLock(ent, args.Used, args.User) || TryLoadBelt(ent, args.Used, args.User))
        {
            args.Handled = true;
            return;
        }
        if (MetaData(args.Used).EntityPrototype?.ID != "CadiaOmnissianAxe")
            return;
        args.Handled = true;
        if (!HasComp<DamageableComponent>(ent) || _damage.GetTotalDamage(ent.Owner) <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cadia-vehicle-ritual-unneeded"), ent, args.User);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(8), new CadiaVehicleRitualDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
        _audio.PlayPvs(RitualSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-ritual-start", ("name", Name(args.User))), ent, PopupType.Medium);
    }

    private void OnRitual(Entity<CadiaVehicleComponent> ent, ref CadiaVehicleRitualDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        _damage.HealEvenly(ent.Owner, -200, origin: args.User);
        _audio.PlayPvs(RitualDoneSound, ent);
        _popup.PopupEntity(Loc.GetString("cadia-vehicle-ritual-done"), ent, PopupType.Medium);
    }

    private void OnExamined(Entity<CadiaVehicleComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Interior)
            args.PushMarkup(Loc.GetString(ent.Comp.Locked ? "cadia-vehicle-examine-locked" : "cadia-vehicle-examine-unlocked"));
        args.PushMarkup(Loc.GetString("cadia-vehicle-examine-crew",
            ("driver", ent.Comp.Driver != null ? 1 : 0),
            ("gunner", ent.Comp.Gunner != null ? 1 : 0),
            ("passengers", ent.Comp.PassengerCount),
            ("max", ent.Comp.Passengers)));
        args.PushMarkup(Loc.GetString("cadia-vehicle-examine-repair"));
    }

    // ═══════════════════════ Ход и башня ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var dirty = now >= _nextDirty;
        if (dirty)
            _nextDirty = now + DirtyRate;

        var query = EntityQueryEnumerator<CadiaVehicleComponent, TransformComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform, out var body))
        {
            Drive((uid, comp), xform, body, frameTime);
            if (dirty && comp.GunPost && comp.Driver is { } postGunner)
                PlaceGunner((uid, comp), postGunner);

            // Башня доворачивает к прицелу с конечной скоростью.
            if (comp.HasTurret)
            {
                var diff = Angle.ShortestDistance(comp.TurretAngle, comp.TurretTarget).Degrees;
                if (Math.Abs(diff) > 0.01)
                {
                    var step = comp.TurretTurnRate * frameTime;
                    comp.TurretAngle = Math.Abs(diff) <= step ? comp.TurretTarget : comp.TurretAngle + Angle.FromDegrees(Math.Sign(diff) * step);
                    if (dirty)
                        Dirty(uid, comp);
                }
            }
            else if (dirty)
            {
                comp.TurretAngle = FireAngle((uid, comp));
            }

            // Автоматическое оружие — пока зажат спуск.
            var shooter = comp.Gunner ?? (comp.DriverIsGunner ? comp.Driver : null);
            if (comp.Firing && shooter is { } gunner && comp.SelectedWeapon < comp.Weapons.Count && comp.Weapons[comp.SelectedWeapon].Automatic)
                TryFire((uid, comp), gunner);
        }
    }

    /// <summary>
    /// Танк: W/S — вдоль корпуса (назад медленнее), A/D — разворот. Шагоход: шаг по направлению, корпус доворачивает.
    /// Без водителя машина стоит.
    /// </summary>
    private void Drive(Entity<CadiaVehicleComponent> ent, TransformComponent xform, PhysicsComponent body, float frameTime)
    {
        var buttons = ent.Comp.Driver is { } driver && TryComp<InputMoverComponent>(driver, out var mover) && _mobState.IsAlive(driver)
            ? mover.HeldMoveButtons
            : MoveButtons.None;

        var rotation = _xform.GetWorldRotation(xform);
        Vector2 velocity;
        if (ent.Comp.TankControls)
        {
            var forward = ((buttons & MoveButtons.Up) != 0 ? 1 : 0) - ((buttons & MoveButtons.Down) != 0 ? 1 : 0);
            var turn = ((buttons & MoveButtons.Left) != 0 ? 1 : 0) - ((buttons & MoveButtons.Right) != 0 ? 1 : 0);
            if (turn != 0)
            {
                rotation += Angle.FromDegrees(turn * ent.Comp.TurnRate * frameTime * (forward < 0 ? -1 : 1));
                _xform.SetWorldRotation(ent, rotation);
            }
            var speed = forward > 0 ? ent.Comp.Speed : forward < 0 ? -ent.Comp.ReverseSpeed : 0f;
            velocity = rotation.RotateVec(new Vector2(0, 1)) * speed;
        }
        else
        {
            var wish = Vector2.Zero;
            if ((buttons & MoveButtons.Up) != 0) wish += new Vector2(0, 1);
            if ((buttons & MoveButtons.Down) != 0) wish -= new Vector2(0, 1);
            if ((buttons & MoveButtons.Right) != 0) wish += new Vector2(1, 0);
            if ((buttons & MoveButtons.Left) != 0) wish -= new Vector2(1, 0);
            velocity = Vector2.Zero;
            if (wish != Vector2.Zero)
            {
                wish = wish.Normalized();
                velocity = wish * ent.Comp.Speed;
                // Угол 0 у направленного спрайта — юг, поэтому к курсу прибавляется разворот.
                var target = new Angle(MathF.Atan2(-wish.X, wish.Y) + MathF.PI);
                var diff = Angle.ShortestDistance(rotation, target).Degrees;
                var step = ent.Comp.TurnRate * frameTime;
                rotation = Math.Abs(diff) <= step ? target : rotation + Angle.FromDegrees(Math.Sign(diff) * step);
                _xform.SetWorldRotation(ent, rotation);
            }
        }
        _physics.SetLinearVelocity(ent, velocity, body: body);
        _physics.SetAngularVelocity(ent, 0f, body: body);

        // Шагоходы идут не через InputMover: анимацию шага переключаем сами.
        if (TryComp<SpriteMovementComponent>(ent, out var walk))
        {
            var moving = velocity.LengthSquared() > 0.01f;
            if (walk.IsMoving != moving)
            {
                walk.IsMoving = moving;
                Dirty(ent, walk);
            }
        }
    }
}
