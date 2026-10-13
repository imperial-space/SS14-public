using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Chat;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// Консоль управления слаймами (camera_advanced/xenobio), процессор слаймов и переработчик обезьян.
/// </summary>
public sealed class XenobioConsoleSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SlimePotionSystem _potion = default!;
    [Dependency] private readonly SlimeSystem _slime = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly XenobioDropTargetSystem _dropTarget = default!;

    private static readonly ProtoId<TagPrototype> MonkeyCubeTag = "MonkeyCube";
    private static readonly EntProtoId SuctionEffect = "XenobioSuctionEffect";
    private static readonly SoundSpecifier AirSuck = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/air_suck.ogg");
    private static readonly SoundSpecifier AirShoot = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/air_shoot.ogg");
    private static readonly SoundSpecifier Shatter = new SoundCollectionSpecifier("GlassBreak");
    private static readonly SoundSpecifier Blender = new SoundPathSpecifier("/Audio/Machines/blender.ogg");
    private static readonly SoundSpecifier Juicer = new SoundPathSpecifier("/Audio/Machines/juicer.ogg");
    private static readonly SoundSpecifier Hiss = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/hiss.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioConsoleComponent, ComponentInit>(OnConsoleInit);
        SubscribeLocalEvent<XenobioConsoleComponent, InteractHandEvent>(OnConsoleInteract);
        SubscribeLocalEvent<XenobioConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<XenobioConsoleComponent, ExaminedEvent>(OnConsoleExamined);
        SubscribeLocalEvent<XenobioConsoleComponent, EntityTerminatingEvent>(OnConsoleTerminating);

        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioCameraOffEvent>(OnCameraOff);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioSlimePlaceEvent>(OnSlimePlace);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioSlimePickUpEvent>(OnSlimePickUp);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioFeedSlimeEvent>(OnFeedSlime);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioMonkeyRecycleEvent>(OnMonkeyRecycle);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioSlimeScanEvent>(OnSlimeScan);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioFeedPotionEvent>(OnFeedPotion);
        SubscribeLocalEvent<XenobioCameraEyeComponent, XenobioHotkeyHelpEvent>(OnHotkeyHelp);
        SubscribeLocalEvent<XenobioCameraEyeComponent, PlayerDetachedEvent>(OnEyeDetached);
        SubscribeLocalEvent<XenobioCameraEyeComponent, EntityTerminatingEvent>(OnEyeTerminating);

        SubscribeLocalEvent<SlimeProcessorComponent, ComponentInit>(OnProcessorInit);
        SubscribeLocalEvent<SlimeProcessorComponent, InteractHandEvent>(OnProcessorInteract);
        SubscribeLocalEvent<SlimeProcessorComponent, DragDropTargetEvent>(OnProcessorDragDrop);
        SubscribeLocalEvent<SlimeProcessorComponent, ExaminedEvent>(OnProcessorExamined);

        SubscribeLocalEvent<SlimeMonkeyRecyclerComponent, DragDropTargetEvent>(OnRecyclerDragDrop);
        SubscribeLocalEvent<SlimeMonkeyRecyclerComponent, InteractHandEvent>(OnRecyclerInteract);
        SubscribeLocalEvent<SlimeMonkeyRecyclerComponent, ExaminedEvent>(OnRecyclerExamined);

        SubscribeLocalEvent<SlimeScannerComponent, AfterInteractEvent>(OnScannerInteract);
    }

    #region Консоль

    private void OnConsoleInit(Entity<XenobioConsoleComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.SlimeContainer);
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.PotionContainer);
    }

    private Container Slimes(Entity<XenobioConsoleComponent> ent)
    {
        return _container.EnsureContainer<Container>(ent, ent.Comp.SlimeContainer);
    }

    private ContainerSlot PotionSlot(Entity<XenobioConsoleComponent> ent)
    {
        return _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.PotionContainer);
    }

    private void OnConsoleExamined(Entity<XenobioConsoleComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("xenobio-console-examine",
            ("slimes", Slimes(ent).ContainedEntities.Count), ("max", ent.Comp.MaxSlimes),
            ("monkeys", ent.Comp.StoredMonkeys)));
        if (PotionSlot(ent).ContainedEntity is { } potion)
            args.PushMarkup(Loc.GetString("xenobio-console-examine-potion", ("potion", potion)));
    }

    /// <summary>Destroy(): слаймы вываливаются наружу.</summary>
    private void OnConsoleTerminating(Entity<XenobioConsoleComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Eye is { } eye && TryComp<XenobioCameraEyeComponent>(eye, out var eyeComp))
            ReturnAndCleanup((eye, eyeComp));

        foreach (var slime in Slimes(ent).ContainedEntities.ToArray())
        {
            _container.Remove(slime, Slimes(ent));
            if (TryComp<SlimeComponent>(slime, out var slimeComp))
                _slime.SetConsoleStasis((slime, slimeComp), false);
        }
    }

    private void OnConsoleInteract(Entity<XenobioConsoleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Eye is { } existing && !TerminatingOrDeleted(existing))
        {
            _popup.PopupEntity(Loc.GetString("xenobio-console-busy"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (!_mind.TryGetMind(args.User, out var mindId, out _))
            return;

        args.Handled = true;
        var eye = Spawn(ent.Comp.EyeProto, _transform.GetMoverCoordinates(ent));
        var eyeComp = EnsureComp<XenobioCameraEyeComponent>(eye);
        eyeComp.Console = ent;
        eyeComp.Mind = mindId;
        eyeComp.Body = args.User;
        eyeComp.LastValid = Transform(eye).Coordinates;
        ent.Comp.Eye = eye;

        _mind.TransferTo(mindId, eye, ghostCheckOverride: true, createGhost: false);
        UpdateHud(ent, eye);
    }

    private void OnConsoleInteractUsing(Entity<XenobioConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // slimepotion_act(): вставка или замена зелья для слаймов.
        if (TryComp<SlimePotionComponent>(args.Used, out var potion) && potion.SlimeOnly)
        {
            args.Handled = true;
            var slot = PotionSlot(ent);
            var old = slot.ContainedEntity;
            if (old != null)
                _container.Remove(old.Value, slot);
            if (!_container.Insert(args.Used, slot))
                return;
            if (old != null)
                _hands.PickupOrDrop(args.User, old.Value);
            _popup.PopupEntity(Loc.GetString(old != null ? "xenobio-console-potion-swapped" : "xenobio-console-potion-inserted"), ent, args.User);
            return;
        }

        // monkeycube_act()
        if (_tag.HasTag(args.Used, MonkeyCubeTag))
        {
            args.Handled = true;
            ent.Comp.StoredMonkeys += 1;
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("xenobio-console-cubes-stored", ("count", ent.Comp.StoredMonkeys)), ent, args.User);
            return;
        }

        // storage_act(): кубики из сумки или коробки.
        if (TryComp<StorageComponent>(args.Used, out var storage))
        {
            var cubes = storage.Container.ContainedEntities.Where(e => _tag.HasTag(e, MonkeyCubeTag)).ToList();
            if (cubes.Count == 0)
                return;

            args.Handled = true;
            foreach (var cube in cubes)
            {
                ent.Comp.StoredMonkeys += 1;
                QueueDel(cube);
            }

            _popup.PopupEntity(Loc.GetString("xenobio-console-cubes-stored", ("count", ent.Comp.StoredMonkeys)), ent, args.User);
        }
    }

    private void UpdateHud(Entity<XenobioConsoleComponent> ent, EntityUid eye)
    {
        var potion = PotionSlot(ent).ContainedEntity;
        _popup.PopupEntity(Loc.GetString("xenobio-console-hud",
                ("slimes", Slimes(ent).ContainedEntities.Count), ("max", ent.Comp.MaxSlimes),
                ("monkeys", ent.Comp.StoredMonkeys),
                ("potion", potion != null ? Name(potion.Value) : Loc.GetString("xenobio-console-no-potion"))),
            eye, eye);
    }

    #endregion

    #region Камера

    private bool TryGetConsole(Entity<XenobioCameraEyeComponent> eye, out Entity<XenobioConsoleComponent> console)
    {
        console = default;
        if (!TryComp<XenobioConsoleComponent>(eye.Comp.Console, out var comp))
            return false;
        console = (eye.Comp.Console, comp);
        return true;
    }

    /// <summary>allowed_area или зона с XENOBIOLOGY_COMPATIBLE (церулеановые чертежи).</summary>
    private bool IsAllowed(Entity<XenobioConsoleComponent> console, MapCoordinates coords)
    {
        var consoleCoords = _transform.GetMapCoordinates(console);
        if (consoleCoords.MapId == coords.MapId && (consoleCoords.Position - coords.Position).Length() <= console.Comp.Range)
            return true;

        var query = EntityQueryEnumerator<XenobioZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zone, out var xform))
        {
            var zoneCoords = _transform.GetMapCoordinates(uid, xform);
            if (zoneCoords.MapId == coords.MapId && (zoneCoords.Position - coords.Position).Length() <= zone.Range)
                return true;
        }

        return false;
    }

    /// <summary>validate_turf().</summary>
    private bool ValidateTurf(Entity<XenobioCameraEyeComponent> eye, Entity<XenobioConsoleComponent> console, out EntityCoordinates coords)
    {
        coords = _transform.GetMoverCoordinates(eye).SnapToGrid(EntityManager);
        if (IsAllowed(console, _transform.ToMapCoordinates(coords)))
            return true;

        _popup.PopupEntity(Loc.GetString("xenobio-console-invalid-area"), eye, eye);
        return false;
    }

    private IEnumerable<EntityUid> OnTile<T>(EntityCoordinates coords) where T : IComponent
    {
        return _lookup.GetEntitiesInRange<T>(coords, 0.5f).Select(e => e.Owner);
    }

    private void OnCameraOff(Entity<XenobioCameraEyeComponent> ent, ref XenobioCameraOffEvent args)
    {
        args.Handled = true;
        ReturnAndCleanup(ent);
    }

    private void OnEyeDetached(Entity<XenobioCameraEyeComponent> ent, ref PlayerDetachedEvent args)
    {
        ReturnAndCleanup(ent);
    }

    private void OnEyeTerminating(Entity<XenobioCameraEyeComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryComp<XenobioConsoleComponent>(ent.Comp.Console, out var console) && console.Eye == ent.Owner)
            console.Eye = null;
    }

    private void ReturnAndCleanup(Entity<XenobioCameraEyeComponent> ent)
    {
        if (TryComp<XenobioConsoleComponent>(ent.Comp.Console, out var console) && console.Eye == ent.Owner)
            console.Eye = null;

        if (!TerminatingOrDeleted(ent.Comp.Body) && !TerminatingOrDeleted(ent.Comp.Mind)
            && TryComp<MindComponent>(ent.Comp.Mind, out var mind) && mind.OwnedEntity == ent.Owner)
        {
            _mind.TransferTo(ent.Comp.Mind, ent.Comp.Body, ghostCheckOverride: true, createGhost: false, mind: mind);
        }

        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    /// <summary>slime_place(): все слаймы из хранилища выплёвываются под камеру.</summary>
    private void OnSlimePlace(Entity<XenobioCameraEyeComponent> ent, ref XenobioSlimePlaceEvent args)
    {
        args.Handled = true;
        if (!TryGetConsole(ent, out var console) || !ValidateTurf(ent, console, out var coords))
            return;

        var container = Slimes(console);
        var slimes = container.ContainedEntities.ToArray();
        if (slimes.Length == 0)
            return;

        SpitOut(coords);
        foreach (var slime in slimes)
        {
            _container.Remove(slime, container, destination: coords);
            if (TryComp<SlimeComponent>(slime, out var slimeComp))
                _slime.SetConsoleStasis((slime, slimeComp), false);
        }

        _popup.PopupCoordinates(Loc.GetString(slimes.Length == 1 ? "xenobio-console-spat-one" : "xenobio-console-spat-many",
            ("count", slimes.Length)), coords, PopupType.Medium);
        UpdateHud(console, ent);
    }

    /// <summary>slime_pickup().</summary>
    private void OnSlimePickUp(Entity<XenobioCameraEyeComponent> ent, ref XenobioSlimePickUpEvent args)
    {
        args.Handled = true;
        if (!TryGetConsole(ent, out var console) || !ValidateTurf(ent, console, out var coords))
            return;

        var container = Slimes(console);
        foreach (var slime in OnTile<SlimeComponent>(coords).ToList())
        {
            if (container.Contains(slime))
                continue;

            if (container.ContainedEntities.Count >= console.Comp.MaxSlimes)
            {
                _popup.PopupEntity(Loc.GetString("xenobio-console-storage-full"), ent, ent);
                break;
            }

            if (TryComp<MindContainerComponent>(slime, out var mind) && mind.HasMind)
            {
                _popup.PopupEntity(Loc.GetString("xenobio-console-wiggled-free"), ent, ent);
                continue;
            }

            var slimeComp = Comp<SlimeComponent>(slime);
            _slime.SetConsoleStasis((slime, slimeComp), true);
            SuckUp(slime);
            _container.Insert(slime, container);
            _popup.PopupCoordinates(Loc.GetString("xenobio-console-sucked-up"), coords);
        }

        UpdateHud(console, ent);
    }

    /// <summary>feed_slime(): из кубика появляется обезьяна-приманка.</summary>
    private void OnFeedSlime(Entity<XenobioCameraEyeComponent> ent, ref XenobioFeedSlimeEvent args)
    {
        args.Handled = true;
        if (!TryGetConsole(ent, out var console) || !ValidateTurf(ent, console, out var coords))
            return;

        // ctrl_click_turf(): сначала пытается переработать мёртвых обезьян.
        if (console.Comp.StoredMonkeys < 1f)
        {
            _popup.PopupEntity(Loc.GetString("xenobio-console-no-monkeys", ("count", console.Comp.StoredMonkeys)), ent, ent);
            return;
        }

        var monkey = Spawn(console.Comp.MonkeyProto, coords);
        var food = EnsureComp<SlimeFoodComponent>(monkey);
        food.Feeder = ent.Comp.Body;
        console.Comp.StoredMonkeys = MathF.Round(console.Comp.StoredMonkeys - 1f, 1);
        SpitOut(coords);
        UpdateHud(console, ent);
    }

    /// <summary>try_recycle_monkeys_on_turf().</summary>
    private void OnMonkeyRecycle(Entity<XenobioCameraEyeComponent> ent, ref XenobioMonkeyRecycleEvent args)
    {
        args.Handled = true;
        if (!TryGetConsole(ent, out var console) || !ValidateTurf(ent, console, out var coords))
            return;

        if (FindRecycler(console) is not { } recycler)
        {
            _popup.PopupEntity(Loc.GetString("xenobio-console-no-recycler"), ent, ent);
            return;
        }

        var found = false;
        foreach (var mob in OnTile<MobStateComponent>(coords).ToList())
        {
            if (!_dropTarget.IsMonkey(mob) || !_mobState.IsDead(mob))
                continue;

            found = true;
            SuckUp(mob);
            console.Comp.StoredMonkeys = MathF.Round(console.Comp.StoredMonkeys + recycler.Comp.CubeProduction, 1);
            _popup.PopupCoordinates(Loc.GetString("xenobio-console-monkey-recycled"), coords);
            QueueDel(mob);
        }

        if (!found)
            _popup.PopupEntity(Loc.GetString("xenobio-console-no-dead-monkey"), ent, ent);
        UpdateHud(console, ent);
    }

    /// <summary>connected_recycler_ref: ближайший переработчик в своей зоне.</summary>
    private Entity<SlimeMonkeyRecyclerComponent>? FindRecycler(Entity<XenobioConsoleComponent> console)
    {
        Entity<SlimeMonkeyRecyclerComponent>? best = null;
        var bestDist = float.MaxValue;
        var origin = _transform.GetMapCoordinates(console);
        foreach (var recycler in _lookup.GetEntitiesInRange<SlimeMonkeyRecyclerComponent>(origin, console.Comp.RecyclerRange))
        {
            var dist = (_transform.GetMapCoordinates(recycler).Position - origin.Position).Length();
            if (dist >= bestDist)
                continue;
            bestDist = dist;
            best = recycler;
        }

        return best;
    }

    private void OnSlimeScan(Entity<XenobioCameraEyeComponent> ent, ref XenobioSlimeScanEvent args)
    {
        args.Handled = true;
        if (!TryGetConsole(ent, out var console) || !ValidateTurf(ent, console, out var coords))
            return;

        foreach (var slime in OnTile<SlimeComponent>(coords).ToList())
        {
            SendScan(ent, (slime, Comp<SlimeComponent>(slime)));
        }
    }

    /// <summary>feed_potion(): зелье из консоли применяется к слайму под камерой.</summary>
    private void OnFeedPotion(Entity<XenobioCameraEyeComponent> ent, ref XenobioFeedPotionEvent args)
    {
        args.Handled = true;
        if (!TryGetConsole(ent, out var console) || !ValidateTurf(ent, console, out var coords))
            return;

        var slot = PotionSlot(console);
        if (slot.ContainedEntity is not { } potion || !TryComp<SlimePotionComponent>(potion, out var potionComp))
        {
            _popup.PopupEntity(Loc.GetString("xenobio-console-no-potion-loaded"), ent, ent);
            return;
        }

        var slime = OnTile<SlimeComponent>(coords).FirstOrDefault();
        if (!slime.IsValid())
            return;

        // spit_atom(): зелье вылетает и разбивается о слайма.
        _container.Remove(potion, slot, destination: coords);
        SpitOut(coords);
        Timer.Spawn(TimeSpan.FromSeconds(0.3), () =>
        {
            if (!TerminatingOrDeleted(potion))
                _audio.PlayPvs(Shatter, coords);
        });

        _potion.Apply((potion, potionComp), slime, ent);
        UpdateHud(console, ent);
    }

    private void OnHotkeyHelp(Entity<XenobioCameraEyeComponent> ent, ref XenobioHotkeyHelpEvent args)
    {
        args.Handled = true;
        SendChat(ent, Loc.GetString("xenobio-console-help"));
    }

    /// <summary>suck_up(): трубка всасывает существо.</summary>
    private void SuckUp(EntityUid target)
    {
        var coords = _transform.GetMoverCoordinates(target);
        Spawn(SuctionEffect, coords);
        Timer.Spawn(TimeSpan.FromSeconds(0.1), () => _audio.PlayPvs(AirSuck, coords));
    }

    /// <summary>spit_out().</summary>
    private void SpitOut(EntityCoordinates coords)
    {
        Spawn(SuctionEffect, coords);
        Timer.Spawn(TimeSpan.FromSeconds(0.1), () => _audio.PlayPvs(AirShoot, coords));
    }

    private void SendScan(EntityUid viewer, Entity<SlimeComponent> slime)
    {
        SendChat(viewer, _slime.ScanReport(slime));
    }

    private void SendChat(EntityUid viewer, string text)
    {
        if (!TryComp<ActorComponent>(viewer, out var actor))
            return;
        _chat.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, actor.PlayerSession.Channel);
    }

    /// <summary>mob/eye/camera/remote/xenobio/setLoc(): камера не покидает свою зону.</summary>
    private void UpdateEyes()
    {
        var query = EntityQueryEnumerator<XenobioCameraEyeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var eye, out var xform))
        {
            if (!TryComp<XenobioConsoleComponent>(eye.Console, out var console))
            {
                ReturnAndCleanup((uid, eye));
                continue;
            }

            if (IsAllowed((eye.Console, console), _transform.GetMapCoordinates(uid, xform)))
            {
                eye.LastValid = xform.Coordinates;
                continue;
            }

            _transform.SetCoordinates(uid, eye.LastValid);
        }
    }

    #endregion

    #region Процессор слаймов

    private void OnProcessorInit(Entity<SlimeProcessorComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.Container);
    }

    private void OnProcessorExamined(Entity<SlimeProcessorComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("slime-processor-examine", ("amount", ent.Comp.RatingAmount), ("speed", ent.Comp.RatingSpeed * 100)));
    }

    private bool CanProcess(EntityUid slime)
    {
        return HasComp<SlimeComponent>(slime);
    }

    private void OnProcessorDragDrop(Entity<SlimeProcessorComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || ent.Comp.Processing || !CanProcess(args.Dragged))
            return;
        args.Handled = true;
        InsertIntoProcessor(ent, args.Dragged, args.User);
    }

    private void InsertIntoProcessor(Entity<SlimeProcessorComponent> ent, EntityUid slime, EntityUid user)
    {
        if (TryComp<PullableComponent>(slime, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(slime, pullable);

        _container.Insert(slime, _container.EnsureContainer<Container>(ent, ent.Comp.Container));
        _popup.PopupEntity(Loc.GetString("slime-processor-insert", ("user", user), ("slime", slime), ("machine", ent.Owner)), ent);
    }

    /// <summary>interact(): затащить слайма или запустить обработку.</summary>
    private void OnProcessorInteract(Entity<SlimeProcessorComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Processing)
        {
            _popup.PopupEntity(Loc.GetString("slime-processor-busy", ("machine", ent.Owner)), ent, args.User);
            return;
        }

        if (TryComp<PullerComponent>(args.User, out var puller) && puller.Pulling is { } pulled && CanProcess(pulled))
        {
            InsertIntoProcessor(ent, pulled, args.User);
            return;
        }

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.Container);
        if (container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("slime-processor-empty", ("machine", ent.Owner)), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("slime-processor-start", ("user", args.User), ("machine", ent.Owner)), ent);
        ent.Comp.Processing = true;
        var total = ent.Comp.TimePerSlime * container.ContainedEntities.Count / ent.Comp.RatingSpeed;
        ent.Comp.ProcessingEnd = _timing.CurTime + TimeSpan.FromSeconds(total);
        _audio.PlayPvs(Blender, ent);
        _appearance.SetData(ent, SlimeProcessorVisuals.Processing, true);
    }

    /// <summary>complete_processing() + process_food().</summary>
    private void CompleteProcessing(Entity<SlimeProcessorComponent> ent)
    {
        ent.Comp.Processing = false;
        _appearance.SetData(ent, SlimeProcessorVisuals.Processing, false);
        var container = _container.EnsureContainer<Container>(ent, ent.Comp.Container);
        var coords = _transform.GetMoverCoordinates(ent);

        foreach (var slime in container.ContainedEntities.ToArray())
        {
            _container.Remove(slime, container, destination: coords);
            if (!TryComp<SlimeComponent>(slime, out var slimeComp))
                continue;

            if (!_mobState.IsDead(slime))
            {
                _popup.PopupEntity(Loc.GetString("slime-processor-crawls-out"), slime);
                continue;
            }

            var type = _slime.CoreFor(slimeComp);
            var count = slimeComp.Cores + ent.Comp.RatingAmount - 1;
            for (var i = 0; i < count; i++)
            {
                Spawn(type, coords.Offset(new System.Numerics.Vector2((i % 4) * 0.25f - 0.375f, (i / 4) * 0.25f - 0.375f)));
            }

            QueueDel(slime);
        }

        _popup.PopupEntity(Loc.GetString("slime-processor-done", ("machine", ent.Owner)), ent);
    }

    /// <summary>processor/slime/process(): сам подбирает мёртвых слаймов рядом.</summary>
    private void UpdateProcessors(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimeProcessorComponent>();
        while (query.MoveNext(out var uid, out var processor))
        {
            if (processor.Processing)
            {
                if (now >= processor.ProcessingEnd)
                    CompleteProcessing((uid, processor));
                continue;
            }

            if (now < processor.NextPickup)
                continue;
            processor.NextPickup = now + TimeSpan.FromSeconds(1);

            var picked = 0;
            var picked1 = new List<EntityUid>();
            foreach (var slime in _lookup.GetEntitiesInRange<SlimeComponent>(_transform.GetMapCoordinates(uid), 1.2f))
            {
                if (!_mobState.IsDead(slime) || _container.IsEntityInContainer(slime))
                    continue;
                picked1.Add(slime);
                if (++picked >= processor.RatingAmount)
                    break;
            }

            if (picked1.Count == 0)
                continue;

            var container = _container.EnsureContainer<Container>(uid, processor.Container);
            foreach (var slime in picked1)
            {
                _container.Insert(slime, container);
            }

            _popup.PopupEntity(Loc.GetString("slime-processor-sucked", ("count", picked1.Count), ("machine", uid)), uid);
        }
    }

    #endregion

    #region Переработчик обезьян

    private void OnRecyclerExamined(Entity<SlimeMonkeyRecyclerComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("monkey-recycler-examine", ("production", ent.Comp.CubeProduction), ("matter", ent.Comp.StoredMatter)));
    }

    /// <summary>stuff_monkey_in().</summary>
    private void OnRecyclerDragDrop(Entity<SlimeMonkeyRecyclerComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !_dropTarget.IsMonkey(args.Dragged))
            return;
        args.Handled = true;

        if (!_mobState.IsIncapacitated(args.Dragged))
        {
            _popup.PopupEntity(Loc.GetString("monkey-recycler-struggling"), ent, args.User);
            return;
        }

        QueueDel(args.Dragged);
        ent.Comp.StoredMatter = MathF.Round(ent.Comp.StoredMatter + ent.Comp.CubeProduction, 1);
        _audio.PlayPvs(Juicer, ent);
        _popup.PopupEntity(Loc.GetString("monkey-recycler-stuffed", ("matter", ent.Comp.StoredMatter)), ent, args.User);
    }

    private void OnRecyclerInteract(Entity<SlimeMonkeyRecyclerComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.StoredMatter < 1f)
        {
            _popup.PopupEntity(Loc.GetString("monkey-recycler-not-enough", ("matter", ent.Comp.StoredMatter)), ent, args.User);
            return;
        }

        _audio.PlayPvs(Hiss, ent);
        var coords = _transform.GetMoverCoordinates(ent);
        while (ent.Comp.StoredMatter >= 1f)
        {
            Spawn(ent.Comp.Cube, coords);
            ent.Comp.StoredMatter = MathF.Round(ent.Comp.StoredMatter - 1f, 1);
        }

        _popup.PopupEntity(Loc.GetString("monkey-recycler-dispensed", ("matter", ent.Comp.StoredMatter)), ent, args.User);
    }

    #endregion

    private void OnScannerInteract(Entity<SlimeScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        if (!TryComp<SlimeComponent>(target, out var slime))
            return;
        args.Handled = true;
        SendScan(args.User, (target, slime));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateEyes();
        UpdateProcessors(_timing.CurTime);
    }
}
