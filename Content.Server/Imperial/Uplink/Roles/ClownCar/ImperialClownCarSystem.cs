using System.Linq;
using System.Numerics;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.ImperialVehicle.Components;
using Content.Shared.Imperial.Uplink.Roles.ClownCar;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Sound.Components;
using Content.Shared.Sprite;
using Content.Shared.Standing;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Uplink.Roles.ClownCar;

/// <summary>
/// /obj/vehicle/sealed/car/clowncar из TG: водить может только клоун, всех остальных машина похищает.
/// </summary>
public sealed class ImperialClownCarSystem : SharedImperialClownCarSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly Robust.Shared.Physics.Systems.SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly BlurryVisionSystem _blurry = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly ProtoId<JobPrototype> ClownJob = "Clown";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> KeyTag = "BikeHorn";
    private static readonly EntProtoId DeerProto = "MobGoat";
    private static readonly EntProtoId FoamProto = "Foam";
    private static readonly EntProtoId SmokeProto = "Smoke";
    private static readonly EntProtoId SpecialPeelProto = "ImperialClownCarSpecialPeel";
    private static readonly ProtoId<ReagentPrototype> LubeReagent = "SpaceLube";
    private static readonly ProtoId<ReagentPrototype> LaughterReagent = "Laughter";
    private static readonly ProtoId<ReagentPrototype> OilReagent = "ImperialSlipperyOil";
    private static readonly ProtoId<ReagentPrototype> BloodReagent = "Blood";
    private static readonly ProtoId<ReagentPrototype> CarBombReagent = "ImperialIrishCarBomb";
    private static readonly HashSet<EntProtoId> Bananas = ["FoodBanana", "FoodMimana"];
    private static readonly string[] Megafauna = ["LavalandMegafauna", "MegafaunaAi"];

    private static readonly EntProtoId HornAction = "ImperialActionClownCarHorn";
    private static readonly EntProtoId HeadlightsAction = "ImperialActionClownCarHeadlights";
    private static readonly EntProtoId DumpAction = "ImperialActionClownCarDump";
    private static readonly EntProtoId RemoveKeyAction = "ImperialActionClownCarRemoveKey";
    private static readonly EntProtoId ExitAction = "ImperialActionClownCarExit";
    private static readonly EntProtoId DiceAction = "ImperialActionClownCarDice";
    private static readonly EntProtoId CannonAction = "ImperialActionClownCarCannon";
    private static readonly EntProtoId FireAction = "ImperialActionClownCarFire";
    private static readonly EntProtoId ThankAction = "ImperialActionClownCarThank";

    private const string Sounds = "/Audio/Imperial/Uplink/ClownCar/";
    private static readonly SoundSpecifier DoorClose = new SoundPathSpecifier(Sounds + "door_close.ogg");
    private static readonly SoundSpecifier DoorOpen = new SoundPathSpecifier(Sounds + "door_open.ogg");
    private static readonly SoundSpecifier ButtonPress = new SoundPathSpecifier(Sounds + "button_press.ogg");
    private static readonly SoundSpecifier CrashPins = new SoundPathSpecifier(Sounds + "clowncar_crashpins.ogg");
    private static readonly SoundSpecifier Fart = new SoundPathSpecifier(Sounds + "clowncar_fart.ogg");
    private static readonly SoundSpecifier CarCrash = new SoundPathSpecifier(Sounds + "car_crash.ogg");
    private static readonly SoundSpecifier Engine = new SoundPathSpecifier(Sounds + "carrev.ogg");
    private static readonly SoundSpecifier Splat = new SoundPathSpecifier(Sounds + "cartoon_splat.ogg");
    private static readonly SoundSpecifier FlashRing = new SoundPathSpecifier(Sounds + "flash_ring.ogg");
    private static readonly SoundSpecifier MagIn = new SoundPathSpecifier(Sounds + "magin.ogg");
    private static readonly SoundSpecifier MagOut = new SoundPathSpecifier(Sounds + "magout.ogg");
    private static readonly SoundSpecifier CannonOn = new SoundPathSpecifier(Sounds + "clowncar_cannonmode1.ogg");
    private static readonly SoundSpecifier CannonOff = new SoundPathSpecifier(Sounds + "clowncar_cannonmode2.ogg");
    private static readonly SoundSpecifier CarHorn = new SoundPathSpecifier("/Audio/Effects/Vehicle/carhorn.ogg");
    private static readonly string[] LoadSounds = ["clowncar_load1.ogg", "clowncar_load2.ogg"];
    private static readonly string[] CrashSounds = ["clowncar_crash1.ogg", "clowncar_crash2.ogg"];
    private static readonly string[] RamSounds = ["clowncar_ram1.ogg", "clowncar_ram2.ogg", "clowncar_ram3.ogg"];
    private static readonly string[] CannonSounds = ["carcannon1.ogg", "carcannon2.ogg", "carcannon3.ogg"];

    private static readonly Color[] HeadlightColors =
    [
        Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.FromHex("#1E90FF"), Color.Cyan, Color.Purple,
    ];

    private static readonly AudioParams Loud = AudioParams.Default.WithVolume(2f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialClownCarComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<ImperialClownCarComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<ImperialClownCarComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ImperialClownCarComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ImperialClownCarComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<ImperialClownCarComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<ImperialClownCarComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<ImperialClownCarComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialClownCarComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<ImperialClownCarComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<ImperialClownCarComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ImperialClownCarComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialClownCarBlurComponent, GetBlurEvent>(OnGetBlur);

        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarForceDoAfterEvent>(OnForceDoAfter);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarEscapeDoAfterEvent>(OnEscapeDoAfter);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarTrunkDoAfterEvent>(OnTrunkDoAfter);

        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarHornEvent>(OnHorn);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarHeadlightsEvent>(OnHeadlights);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarDumpEvent>(OnDump);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarRemoveKeyEvent>(OnRemoveKey);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarExitEvent>(OnExit);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarDiceEvent>(OnDice);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarCannonEvent>(OnCannon);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarFireEvent>(OnFire);
        SubscribeLocalEvent<ImperialClownCarComponent, ImperialClownCarThankEvent>(OnThank);
    }

    #region Посадка и высадка

    private Container Victims(Entity<ImperialClownCarComponent> ent)
        => _container.EnsureContainer<Container>(ent, ent.Comp.Container);

    private EntityUid? Driver(EntityUid car)
        => TryComp<ImperialVehicleComponent>(car, out var vehicle) ? vehicle.Rider : null;

    private int Occupants(Entity<ImperialClownCarComponent> ent)
        => Victims(ent).ContainedEntities.Count + (Driver(ent) != null ? 1 : 0);

    private bool IsClown(EntityUid uid)
        => _mind.TryGetMind(uid, out var mindId, out _)
           && _jobs.MindTryGetJobId(mindId, out var job)
           && job == ClownJob;

    protected override void TryStartEnter(Entity<ImperialClownCarComponent> ent, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(user) || Occupants(ent) >= ent.Comp.MaxOccupants)
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.EnterDelay,
            new ImperialClownCarEnterDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            DistanceThreshold = 1.5f,
        });
    }

    protected override void TryStartForcedEnter(Entity<ImperialClownCarComponent> ent, EntityUid user, EntityUid target)
    {
        if (!HasComp<MobStateComponent>(target) || Occupants(ent) >= ent.Comp.MaxOccupants)
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-forcing", ("user", user), ("target", target), ("car", ent)),
            target, PopupType.MediumCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.EnterDelay,
            new ImperialClownCarForceDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            DistanceThreshold = 1.5f,
        });
    }

    private void OnEnterDoAfter(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Occupants(ent) >= ent.Comp.MaxOccupants)
            return;

        args.Handled = true;
        var user = args.User;

        // auto_assign_occupant_flags: клоун садится за руль, если он свободен.
        if (Driver(ent) == null && IsClown(user) && HasComp<BuckleComponent>(user))
        {
            ent.Comp.AllowedDriver = user;
            var buckled = _buckle.TryBuckle(user, user, ent);
            ent.Comp.AllowedDriver = null;
            if (buckled)
                return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-climbs-in", ("user", user), ("car", ent)), ent);
        _audio.PlayPvs(DoorClose, ent);
        Insert(ent, user);
    }

    private void OnForceDoAfter(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarForceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        ForcedEnter(ent, target);
    }

    /// <summary>mob_forced_enter.</summary>
    private void ForcedEnter(Entity<ImperialClownCarComponent> ent, EntityUid victim)
    {
        if (Occupants(ent) >= ent.Comp.MaxOccupants)
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-forced-in", ("target", victim), ("car", ent)), victim, PopupType.MediumCaution);
        _audio.PlayPvs(new SoundPathSpecifier(Sounds + _random.Pick(LoadSounds)), ent, Loud);
        if (!Insert(ent, victim))
            return;

        CheckCarBomb(ent, victim);
    }

    private bool Insert(Entity<ImperialClownCarComponent> ent, EntityUid mob)
    {
        if (TryComp<BuckleComponent>(mob, out var buckle) && buckle.BuckledTo != null)
            _buckle.TryUnbuckle((mob, buckle), mob, popup: false);

        return _container.Insert(mob, Victims(ent));
    }

    private void OnStrapped(Entity<ImperialClownCarComponent> ent, ref StrappedEvent args)
    {
        var driver = args.Buckle.Owner;
        ent.Comp.Driver = driver;
        _audio.PlayPvs(DoorClose, ent);
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-climbs-in", ("user", driver), ("car", ent)), ent);

        _actions.AddAction(driver, ref ent.Comp.HornAction, HornAction, ent);
        _actions.AddAction(driver, ref ent.Comp.HeadlightsAction, HeadlightsAction, ent);
        _actions.AddAction(driver, ref ent.Comp.DumpAction, DumpAction, ent);
        _actions.AddAction(driver, ref ent.Comp.RemoveKeyAction, RemoveKeyAction, ent);
        _actions.AddAction(driver, ref ent.Comp.ExitAction, ExitAction, ent);
        if (ent.Comp.Emagged)
        {
            _actions.AddAction(driver, ref ent.Comp.DiceAction, DiceAction, ent);
            _actions.AddAction(driver, ref ent.Comp.CannonAction, CannonAction, ent);
        }

        if (ent.Comp.Cannon == ImperialClownCannonMode.Ready)
            _actions.AddAction(driver, ref ent.Comp.FireAction, FireAction, ent);

        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnUnstrapped(Entity<ImperialClownCarComponent> ent, ref UnstrappedEvent args)
    {
        var driver = args.Buckle.Owner;
        if (ent.Comp.Driver != driver)
            return;

        ent.Comp.Driver = null;
        foreach (var action in new[]
                 {
                     ent.Comp.HornAction, ent.Comp.HeadlightsAction, ent.Comp.DumpAction, ent.Comp.RemoveKeyAction,
                     ent.Comp.ExitAction, ent.Comp.DiceAction, ent.Comp.CannonAction, ent.Comp.FireAction,
                 })
        {
            RemoveAttached(driver, action);
        }

        if (!TerminatingOrDeleted(ent))
            _audio.PlayPvs(DoorOpen, ent);
    }

    private void RemoveAttached(EntityUid performer, EntityUid? action)
    {
        if (TryComp<ActionComponent>(action, out var comp) && comp.AttachedEntity == performer)
            _actions.RemoveAction(performer, action);
    }

    private void OnInserted(Entity<ImperialClownCarComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container)
            return;

        var victim = args.Entity;
        var kidnapped = EnsureComp<ImperialClownCarKidnappedComponent>(victim);
        kidnapped.Car = ent;
        _actions.AddAction(victim, ref kidnapped.ThankAction, ThankAction, ent);
        _actions.AddAction(victim, ref kidnapped.ExitAction, ExitAction, ent);

        // after_add_occupant: ачивка за 30 похищенных — достижений у нас нет.
    }

    private void OnRemoved(Entity<ImperialClownCarComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container
            || !TryComp<ImperialClownCarKidnappedComponent>(args.Entity, out var kidnapped))
            return;

        _actions.RemoveAction(args.Entity, kidnapped.ThankAction);
        _actions.RemoveAction(args.Entity, kidnapped.ExitAction);
        QueueDel(kidnapped.ThankAction);
        QueueDel(kidnapped.ExitAction);
        RemComp(args.Entity, kidnapped);
    }

    private void OnRelayMovement(Entity<ImperialClownCarComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (HasComp<ImperialClownCarKidnappedComponent>(args.Entity))
            StartEscape(ent, args.Entity);
    }

    /// <summary>mob_try_exit похищенного: escape_time.</summary>
    private void StartEscape(Entity<ImperialClownCarComponent> ent, EntityUid victim)
    {
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, victim, ent.Comp.EscapeDelay,
            new ImperialClownCarEscapeDoAfterEvent(), ent, ent)
        {
            BlockDuplicate = true,
            CancelDuplicate = false,
        });

        if (started)
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-escape-start", ("car", ent)), victim, victim);
    }

    private void OnEscapeDoAfter(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarEscapeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !Victims(ent).Contains(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-escaped", ("user", args.User), ("car", ent)), args.User, PopupType.Medium);
        Exit(ent, args.User, false);
    }

    protected override void TryOpenTrunk(Entity<ImperialClownCarComponent> ent, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(user))
            return;

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.TrunkDelay,
            new ImperialClownCarTrunkDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
            DistanceThreshold = 1.5f,
        });

        if (started)
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-trunk-start", ("car", ent)), user, user);
    }

    private void OnTrunkDoAfter(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarTrunkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (Victims(ent).ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-trunk-empty", ("car", ent)), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-trunk-tumble", ("car", ent)), args.User, args.User);
        DumpKidnapped(ent);
    }

    /// <summary>mob_exit: выпустить одного, при randomstep — отбросить на соседнюю клетку.</summary>
    private void Exit(Entity<ImperialClownCarComponent> ent, EntityUid mob, bool randomStep)
    {
        if (Driver(ent) == mob)
            _buckle.TryUnbuckle(mob, mob, popup: false);
        else if (!_container.Remove(mob, Victims(ent)))
            return;

        // Водитель был прицеплен к машине и унаследовал бы её скорость.
        _physics.SetLinearVelocity(mob, Vector2.Zero);

        if (!randomStep)
            return;

        var dir = _random.Pick(new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY });
        _throwing.TryThrow(mob, Transform(ent).Coordinates.Offset(dir), 10f, playSound: false);
    }

    /// <summary>dump_mobs / dump_specific_mobs: всех наружу и парализовать на 4 секунды.</summary>
    private void DumpMobs(Entity<ImperialClownCarComponent> ent, bool includeDriver, bool randomStep = true)
    {
        var mobs = Victims(ent).ContainedEntities.ToList();
        if (includeDriver && Driver(ent) is { } driver)
            mobs.Add(driver);

        foreach (var mob in mobs)
        {
            Exit(ent, mob, randomStep);
            if (HasComp<HumanoidProfileComponent>(mob))
                _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(4));
        }
    }

    private void DumpKidnapped(Entity<ImperialClownCarComponent> ent) => DumpMobs(ent, false);

    #endregion

    #region Действия

    private bool IsDriver(Entity<ImperialClownCarComponent> ent, EntityUid user) => Driver(ent) == user;

    private void OnHorn(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarHornEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer))
            return;

        args.Handled = true;
        var now = _timing.CurTime;
        if (now < ent.Comp.NextHorn)
            return;

        ent.Comp.NextHorn = now + TimeSpan.FromSeconds(2);
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-honks", ("car", ent)), ent, PopupType.Medium);

        // Гудок-ключ в замке гудит сам вместо клаксона.
        if (GetKey(ent) is { } key && TryComp<EmitSoundOnUseComponent>(key, out var emit) && emit.Sound != null)
            _audio.PlayPvs(emit.Sound, ent);
        else
            _audio.PlayPvs(CarHorn, ent);
    }

    private void OnHeadlights(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarHeadlightsEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer))
            return;

        args.Handled = true;
        ent.Comp.Headlights = !ent.Comp.Headlights;
        _light.SetEnabled(ent, ent.Comp.Headlights);
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-headlights"), args.Performer, args.Performer);
        _audio.PlayPvs(ent.Comp.Headlights ? MagIn : MagOut, ent, AudioParams.Default.WithVolume(-4f));
    }

    private void OnDump(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarDumpEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-dumping", ("car", ent)), ent, PopupType.Medium);
        DumpKidnapped(ent);
    }

    private void OnExit(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarExitEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (IsDriver(ent, args.Performer))
            Exit(ent, args.Performer, false);
        else if (HasComp<ImperialClownCarKidnappedComponent>(args.Performer))
            StartEscape(ent, args.Performer);
    }

    private void OnThank(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarThankEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (Driver(ent) is not { } driver)
        {
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-thank-nobody"), args.Performer, args.Performer, PopupType.MediumCaution);
            return;
        }

        _chat.TrySendInGameICMessage(args.Performer,
            Loc.GetString("imperial-clowncar-thank", ("driver", Name(driver))), InGameICChatType.Speak, false,
            ignoreActionBlocker: true);
        ent.Comp.ThanksCount++;
    }

    #endregion

    #region Ключ, починка, урон

    private EntityUid? GetKey(Entity<ImperialClownCarComponent> ent)
        => _container.TryGetContainer(ent, ent.Comp.KeyContainer, out var keys) && keys.ContainedEntities.Count > 0
            ? keys.ContainedEntities[0]
            : null;

    private void OnRemoveKey(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarRemoveKeyEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer))
            return;

        args.Handled = true;
        if (GetKey(ent) is not { } key)
        {
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-no-key", ("car", ent)), args.Performer, args.Performer);
            return;
        }

        _container.Remove(key, _container.GetContainer(ent, ent.Comp.KeyContainer));
        _hands.PickupOrDrop(args.Performer, key);
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-key-removed", ("key", key), ("car", ent)), args.Performer, args.Performer);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnInteractUsing(Entity<ImperialClownCarComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;

        // item_interaction: банан чинит машину на свою мощность.
        if (MetaData(used).EntityPrototype is { } proto && Bananas.Contains(proto.ID))
        {
            args.Handled = true;
            var potency = TryComp<ProduceComponent>(used, out var produce) && _botany.TryGetSeed(produce, out var seed)
                ? seed.Potency
                : 0f;
            // Сорт TG начинает с мощности 10, в SS14 — с 1.
            potency = Math.Max(10f, potency);
            Repair(ent, potency);
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-repair", ("item", used), ("car", ent)), args.User, args.User);
            QueueDel(used);
            return;
        }

        if (!_tag.HasTag(used, KeyTag) || GetKey(ent) != null)
            return;

        args.Handled = true;
        if (!_container.Insert(used, _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.KeyContainer)))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-key-inserted", ("key", used), ("car", ent)), args.User, args.User);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void Repair(EntityUid car, float amount)
    {
        if (!TryComp<DamageableComponent>(car, out var damageable))
            return;

        var total = _damageable.GetTotalDamage((car, damageable)).Float();
        if (total <= 0f)
            return;

        var heal = new DamageSpecifier(_damageable.GetAllDamage((car, damageable))) * -Math.Min(1f, amount / total);
        _damageable.TryChangeDamage((car, damageable), heal, true, false);
    }

    private void OnRefreshSpeed(Entity<ImperialClownCarComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // relaymove: без гудка в замке и в режиме пушки машина не едет.
        if (GetKey(ent) == null || ent.Comp.Cannon != ImperialClownCannonMode.Inactive)
            args.ModifySpeed(0f, 0f);
    }

    private void OnDamaged(Entity<ImperialClownCarComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !_random.Prob(0.33f))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-lube", ("car", ent)), ent, PopupType.MediumCaution);
        SpawnFluid(ent, FoamProto, LubeReagent, 25, 4);
    }

    /// <summary>do_foam/do_chem_smoke(range): облако расходится на range клеток ромбом.</summary>
    private void SpawnFluid(EntityUid car, EntProtoId proto, string reagent, float volume, int range)
    {
        var coords = Transform(car).Coordinates;
        var cloud = Spawn(proto, coords);
        var solution = new Solution();
        solution.AddReagent(reagent, volume);
        var spread = Math.Min(300, 2 * range * range + 2 * range + 1);
        _smoke.StartSmoke(cloud, solution, 10f, spread);
    }

    private void OnExamined(Entity<ImperialClownCarComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(GetKey(ent) is { } key
            ? Loc.GetString("imperial-clowncar-examine-key", ("key", key))
            : Loc.GetString("imperial-clowncar-examine-no-key"));

        var count = Victims(ent).ContainedEntities.Count;
        if (count > 0)
            args.PushMarkup(Loc.GetString("imperial-clowncar-examine-victims", ("count", count)));
    }

    private void OnDestruction(Entity<ImperialClownCarComponent> ent, ref DestructionEventArgs args)
    {
        _audio.PlayPvs(Fart, ent, Loud);
        DumpMobs(ent, true);
    }

    #endregion

    private void OnGetBlur(EntityUid uid, ImperialClownCarBlurComponent comp, GetBlurEvent args)
    {
        args.Blur += 4f;
    }

    #region Ирландская бомба

    private void CheckCarBomb(Entity<ImperialClownCarComponent> ent, EntityUid victim)
    {
        var amount = 0f;
        foreach (var solution in MobSolutions(victim))
        {
            var contents = solution.Comp.Solution;
            var quantity = contents.GetTotalPrototypeQuantity(CarBombReagent);
            if (quantity <= 0)
                continue;

            amount += quantity.Float();
            _solutions.RemoveReagent(solution, CarBombReagent, quantity);
        }

        if (amount < 30f || ent.Comp.CarBombAt != null)
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-rattling"), ent, PopupType.LargeCaution);
        ent.Comp.CarBombAt = _timing.CurTime + TimeSpan.FromSeconds(5);
    }

    private IEnumerable<Entity<SolutionComponent>> MobSolutions(EntityUid mob)
    {
        if (TryComp<SolutionContainerManagerComponent>(mob, out var manager))
        {
            foreach (var (_, solution) in _solutions.EnumerateSolutions((mob, manager)))
                yield return solution;
        }

        if (!HasComp<BodyComponent>(mob) || !_body.TryGetOrgansWithComponent<StomachComponent>(mob, out var stomachs))
            yield break;

        foreach (var stomach in stomachs)
        {
            if (!TryComp<SolutionContainerManagerComponent>(stomach, out var stomachManager))
                continue;

            foreach (var (_, solution) in _solutions.EnumerateSolutions((stomach.Owner, stomachManager)))
                yield return solution;
        }
    }

    #endregion

    #region Таран

    private void OnCollide(Entity<ImperialClownCarComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixture.Hard || !args.OtherFixture.Hard || Driver(ent) == null)
            return;

        var other = args.OtherEntity;

        // Машина открывает двери доступом водителя и похищенных (access_provider_flags).
        if (TryComp<DoorComponent>(other, out var door))
        {
            foreach (var occupant in Victims(ent).ContainedEntities.Append(Driver(ent)!.Value))
            {
                if (!_access.IsAllowed(occupant, other))
                    continue;

                _door.TryOpen(other, door, occupant);
                break;
            }

            return;
        }

        if (args.OurBody.LinearVelocity.Length() < 0.5f || !_tag.HasTag(other, WallTag))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-crash", ("car", ent), ("target", other)), ent, PopupType.LargeCaution);
        _audio.PlayPvs(new SoundPathSpecifier(Sounds + _random.Pick(CrashSounds)), ent, Loud);
        _audio.PlayPvs(CrashPins, ent, Loud);
        DumpMobs(ent, true);
    }

    /// <summary>
    /// Bump по живым: мобы в SS14 не сталкиваются физически, поэтому стоящих перед машиной ищем рядом с ней.
    /// Лежащих машина переезжает (check_crossed).
    /// </summary>
    private void CheckMobs(Entity<ImperialClownCarComponent> ent, EntityUid driver)
    {
        var xform = Transform(ent);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(xform.Coordinates, 0.5f))
        {
            var uid = mob.Owner;
            if (uid == driver || uid == ent.Owner || _container.IsEntityInContainer(uid))
                continue;

            if (_standing.IsDown(uid))
            {
                RunOver(ent, uid);
                continue;
            }

            if (IsMegafauna(uid))
                continue;

            if (MetaData(uid).EntityPrototype?.ID != DeerProto.Id)
            {
                if (HasComp<HumanoidProfileComponent>(uid))
                    _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(4));

                _popup.PopupEntity(Loc.GetString("imperial-clowncar-ram", ("car", ent), ("target", uid)), uid, PopupType.LargeCaution);
                ForcedEnter(ent, uid);
                _audio.PlayPvs(new SoundPathSpecifier(Sounds + _random.Pick(RamSounds)), ent, Loud);
                continue;
            }

            HitDeer(ent, uid);
            return;
        }
    }

    private bool IsMegafauna(EntityUid uid)
    {
        foreach (var name in Megafauna)
        {
            if (_compFactory.TryGetRegistration(name, out var reg) && HasComp(uid, reg.Type))
                return true;
        }

        return false;
    }

    /// <summary>Олень (у нас — козёл): машина разбивается, пассажиры калечатся, зверь получает 200 урона.</summary>
    private void HitDeer(Entity<ImperialClownCarComponent> ent, EntityUid deer)
    {
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-careens", ("car", ent), ("target", deer)), ent, PopupType.LargeCaution);

        var occupants = Victims(ent).ContainedEntities.ToList();
        if (Driver(ent) is { } driver)
            occupants.Add(driver);

        foreach (var occupant in occupants)
        {
            if (!HasComp<HumanoidProfileComponent>(occupant) || _random.Prob(0.35f))
                continue;

            // Тупая травма головы средней или тяжёлой степени.
            var blunt = new DamageSpecifier();
            blunt.DamageDict["Blunt"] = _random.Prob(0.5f) ? 15 : 25;
            _damageable.TryChangeDamage(occupant, blunt, true);
            _audio.PlayEntity(FlashRing, occupant, occupant);
            var blur = EnsureComp<ImperialClownCarBlurComponent>(occupant);
            var blurEnd = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(10, 21));
            if (blurEnd > blur.EndAt)
                blur.EndAt = blurEnd;
            _blurry.UpdateBlurMagnitude(occupant);
        }

        var blood = new Solution();
        blood.AddReagent(BloodReagent, 15);
        _puddle.TrySpillAt(Transform(deer).Coordinates, blood, out _, false);

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 200;
        _damageable.TryChangeDamage(deer, damage, true);

        DumpMobs(ent, true);
        _audio.PlayPvs(CarCrash, ent, Loud);
    }

    private void RunOver(Entity<ImperialClownCarComponent> ent, EntityUid target)
    {
        if (!HasComp<HumanoidProfileComponent>(target)
            || HasComp<ImperialClownCarSquishedComponent>(target)
            || !_mobState.IsAlive(target)
            || HasComp<StunnedComponent>(target)
            || _gravity.IsWeightless(ent.Owner))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-clowncar-run-over", ("car", ent), ("target", target)), target, PopupType.MediumCaution);

        // /datum/element/squish: лежащего растягивает вширь и сплющивает на 5 секунд.
        var squished = AddComp<ImperialClownCarSquishedComponent>(target);
        squished.EndAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        squished.OldScale = _scale.GetSpriteScale(target);
        _scale.SetSpriteScale(target, squished.OldScale * new Vector2(7f / 5f, 5f / 7f));

        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(2));
        _audio.PlayPvs(Splat, target, Loud);
    }

    #endregion

    #region Емаг: кнопки и пушка

    private void OnEmagged(Entity<ImperialClownCarComponent> ent, ref GotEmaggedEvent args)
    {
        if (args.Handled || ent.Comp.Emagged || !_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        args.Handled = true;
        ent.Comp.Emagged = true;
        _popup.PopupEntity(Loc.GetString("imperial-clowncar-emagged", ("car", ent)), ent, args.UserUid, PopupType.Medium);

        if (Driver(ent) is { } driver)
        {
            _actions.AddAction(driver, ref ent.Comp.DiceAction, DiceAction, ent);
            _actions.AddAction(driver, ref ent.Comp.CannonAction, CannonAction, ent);
        }
    }

    /// <summary>roll_the_dice: шесть случайных эффектов.</summary>
    private void OnDice(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarDiceEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer))
            return;

        args.Handled = true;
        _audio.PlayPvs(ButtonPress, ent);
        var now = _timing.CurTime;
        if (now < ent.Comp.NextDice)
        {
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-dice-recharging"), args.Performer, args.Performer);
            return;
        }

        ent.Comp.NextDice = now + ent.Comp.DiceCooldown;
        var user = args.Performer;
        var roll = _random.Next(1, 7);
        _popup.PopupEntity(Loc.GetString($"imperial-clowncar-dice-{roll}", ("user", user), ("car", ent)), ent, PopupType.LargeCaution);

        switch (roll)
        {
            case 1:
                Spawn(SpecialPeelProto, Transform(ent).Coordinates);
                break;
            case 2:
                SpawnFluid(ent, FoamProto, RandomReagent(), 100, 200);
                break;
            case 3:
                ent.Comp.SingularityEndAt = now + TimeSpan.FromSeconds(10);
                _appearance.SetData(ent, ImperialClownCarVisuals.Singularity, true);
                break;
            case 4:
                SpawnFluid(ent, SmokeProto, LaughterReagent, 50, 4);
                break;
            case 5:
                ent.Comp.OilEndAt = now + TimeSpan.FromSeconds(3);
                ent.Comp.LastOilTile = null;
                break;
            case 6:
                _audio.PlayPvs(Fart, ent, Loud);
                var laughers = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, 6f)
                    .Select(m => m.Owner)
                    .Concat(Victims(ent).ContainedEntities)
                    .Append(user)
                    .Distinct()
                    .ToList();
                foreach (var mob in laughers)
                {
                    _chat.TryEmoteWithChat(mob, "Laugh", ignoreActionBlocker: true);
                }
                break;
        }
    }

    /// <summary>get_random_reagent_id: любой реагент, кроме абстрактных и админских.</summary>
    private string RandomReagent()
    {
        var reagents = _proto.EnumeratePrototypes<ReagentPrototype>()
            .Where(r => !r.Abstract && !r.ID.Contains("Admin"))
            .Select(r => r.ID)
            .ToList();
        return _random.Pick(reagents);
    }

    /// <summary>toggle_cannon: 2 секунды на раскрытие, в режиме пушки машина стоит.</summary>
    private void OnCannon(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarCannonEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer))
            return;

        args.Handled = true;
        if (ent.Comp.Cannon == ImperialClownCannonMode.Busy)
        {
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-cannon-busy"), args.Performer, args.Performer);
            return;
        }

        var now = _timing.CurTime;
        if (ent.Comp.Cannon == ImperialClownCannonMode.Ready)
        {
            SetState(ent, ImperialClownCarState.FromFire);
            ent.Comp.CannonVisualAt = now + TimeSpan.FromSeconds(1.8);
            _audio.PlayPvs(CannonOff, ent, Loud);
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-cannon-off", ("car", ent)), ent, PopupType.Medium);
            RemoveAttached(args.Performer, ent.Comp.FireAction);
            ent.Comp.CannonActivating = false;
        }
        else
        {
            SetState(ent, ImperialClownCarState.ToFire);
            ent.Comp.CannonVisualAt = now + TimeSpan.FromSeconds(1.8);
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-cannon-on", ("car", ent)), ent, PopupType.MediumCaution);
            _audio.PlayPvs(CannonOn, ent, Loud);
            ent.Comp.CannonActivating = true;
        }

        ent.Comp.Cannon = ImperialClownCannonMode.Busy;
        ent.Comp.CannonSwitchAt = now + TimeSpan.FromSeconds(2);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    /// <summary>fire_cannon_at: выстреливает случайным похищенным в цель.</summary>
    private void OnFire(Entity<ImperialClownCarComponent> ent, ref ImperialClownCarFireEvent args)
    {
        if (args.Handled || !IsDriver(ent, args.Performer) || ent.Comp.Cannon != ImperialClownCannonMode.Ready)
            return;

        var victims = Victims(ent).ContainedEntities;
        if (victims.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-clowncar-cannon-empty"), args.Performer, args.Performer);
            return;
        }

        args.Handled = true;
        var sod = _random.Pick(victims);
        _container.Remove(sod, Victims(ent));
        SetState(ent, ImperialClownCarState.Recoil);
        ent.Comp.RecoilEndAt = _timing.CurTime + TimeSpan.FromSeconds(0.3);
        _audio.PlayPvs(new SoundPathSpecifier(Sounds + _random.Pick(CannonSounds)), ent, Loud);

        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(args.Target);
        var dir = to.Position - from.Position;
        if (dir.LengthSquared() < 0.01f)
            dir = Vector2.UnitX;

        _throwing.TryThrow(sod, Vector2.Normalize(dir) * Math.Min(10f, dir.Length()), 10f, args.Performer);
    }

    private void SetState(EntityUid car, ImperialClownCarState state)
        => _appearance.SetData(car, ImperialClownCarVisuals.State, state);

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<ImperialClownCarComponent, ImperialVehicleComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var car, out var vehicle, out var physics))
        {
            var ent = (uid, car);

            if (car.CarBombAt is { } bombAt && now >= bombAt)
            {
                car.CarBombAt = null;
                DumpMobs(ent, true);
                _explosion.QueueExplosion(uid, "Default", 10f, 5f, 3f, canCreateVacuum: false);
                continue;
            }

            if (car.Headlights && car.Emagged && now >= car.NextPartyColor)
            {
                car.NextPartyColor = now + TimeSpan.FromSeconds(1);
                _light.SetColor(uid, _random.Pick(HeadlightColors));
            }

            if (car.SingularityEndAt is { } singEnd && now >= singEnd)
            {
                car.SingularityEndAt = null;
                _appearance.SetData(uid, ImperialClownCarVisuals.Singularity, false);
            }

            if (car.RecoilEndAt is { } recoilEnd && now >= recoilEnd)
            {
                car.RecoilEndAt = null;
                if (car.Cannon == ImperialClownCannonMode.Ready)
                    SetState(uid, ImperialClownCarState.Fire);
            }

            if (car.CannonVisualAt is { } visualAt && now >= visualAt)
            {
                car.CannonVisualAt = null;
                var state = _appearance.TryGetData<ImperialClownCarState>(uid, ImperialClownCarVisuals.State, out var cur)
                            && cur == ImperialClownCarState.ToFire
                    ? ImperialClownCarState.Fire
                    : ImperialClownCarState.Normal;
                SetState(uid, state);
            }

            if (car.CannonSwitchAt is { } switchAt && now >= switchAt)
            {
                car.CannonSwitchAt = null;
                var activating = car.CannonActivating;
                car.Cannon = activating ? ImperialClownCannonMode.Ready : ImperialClownCannonMode.Inactive;
                if (activating && vehicle.Rider is { } gunner)
                    _actions.AddAction(gunner, ref car.FireAction, FireAction, uid);
                _speed.RefreshMovementSpeedModifiers(uid);
            }

            if (vehicle.Rider is not { } driver || physics.LinearVelocity.Length() < 0.5f)
                continue;

            if (now >= car.NextEngineSound)
            {
                car.NextEngineSound = now + TimeSpan.FromSeconds(2);
                _audio.PlayPvs(Engine, uid);
            }

            if (car.OilEndAt is { } oilEnd)
            {
                if (now >= oilEnd)
                {
                    car.OilEndAt = null;
                }
                else
                {
                    var tile = Transform(uid).Coordinates.SnapToGrid(EntityManager);
                    if (car.LastOilTile != tile)
                    {
                        car.LastOilTile = tile;
                        var oil = new Solution();
                        oil.AddReagent(OilReagent, 5);
                        _puddle.TrySpillAt(tile, oil, out _, false);
                    }
                }
            }

            CheckMobs(ent, driver);
        }

        var clearBlur = new List<EntityUid>();
        var blurs = EntityQueryEnumerator<ImperialClownCarBlurComponent>();
        while (blurs.MoveNext(out var uid, out var blur))
        {
            if (now >= blur.EndAt)
                clearBlur.Add(uid);
        }

        foreach (var uid in clearBlur)
        {
            RemComp<ImperialClownCarBlurComponent>(uid);
            _blurry.UpdateBlurMagnitude(uid);
        }

        var squished = EntityQueryEnumerator<ImperialClownCarSquishedComponent>();
        while (squished.MoveNext(out var uid, out var squish))
        {
            if (now < squish.EndAt)
                continue;

            _scale.SetSpriteScale(uid, squish.OldScale);
            RemCompDeferred(uid, squish);
        }
    }
}
