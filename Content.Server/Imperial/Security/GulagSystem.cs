using System.Linq;
using Content.Server.CriminalRecords.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.CriminalRecords;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// machinery/gulag_teleporter и computer/prisoner/gulag_teleporter_computer:
/// раздевает заключённого, выдаёт робу и ID с целью и отправляет его на маяк трудового лагеря.
/// </summary>
public sealed class GulagSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly LaborCampMapSystem _campMap = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SecurityImplantSystem _securityImplant = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
    private static readonly SoundSpecifier DepartSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundSpecifier ArriveSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    private TimeSpan _nextUiUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GulagTeleporterComponent, ActivateInWorldEvent>(OnTeleporterActivate);
        SubscribeLocalEvent<GulagTeleporterComponent, DragDropTargetEvent>(OnTeleporterDragDrop);
        SubscribeLocalEvent<GulagTeleporterComponent, GetVerbsEvent<AlternativeVerb>>(OnTeleporterVerbs);
        SubscribeLocalEvent<GulagTeleporterComponent, ContainerRelayMovementEntityEvent>(OnTeleporterRelayMove);
        SubscribeLocalEvent<GulagTeleporterComponent, GulagBreakoutDoAfterEvent>(OnBreakout);
        SubscribeLocalEvent<GulagTeleporterComponent, EntInsertedIntoContainerMessage>(OnTeleporterContainerChanged);
        SubscribeLocalEvent<GulagTeleporterComponent, EntRemovedFromContainerMessage>(OnTeleporterContainerChanged);
        SubscribeLocalEvent<GulagTeleporterComponent, ComponentShutdown>(OnTeleporterShutdown);

        SubscribeLocalEvent<GulagConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<GulagConsoleComponent, BoundUIOpenedEvent>(OnConsoleUiOpened);
        SubscribeLocalEvent<GulagConsoleComponent, GulagConsoleActionMessage>(OnConsoleAction);
        SubscribeLocalEvent<GulagConsoleComponent, GulagConsoleSetGoalMessage>(OnConsoleSetGoal);
        SubscribeLocalEvent<GulagConsoleComponent, EntInsertedIntoContainerMessage>(OnConsoleSlotChanged);
        SubscribeLocalEvent<GulagConsoleComponent, EntRemovedFromContainerMessage>(OnConsoleSlotChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUiUpdate)
            return;

        _nextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<GulagConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, GulagConsoleUiKey.Key))
                UpdateConsoleUi((uid, console));
        }
    }

    #region Телепорт

    /// <summary>interact(): открыть или закрыть дверь, если не заперто.</summary>
    private void OnTeleporterActivate(Entity<GulagTeleporterComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (ent.Comp.Locked)
        {
            _popup.PopupEntity(Loc.GetString("gulag-teleporter-locked", ("machine", ent.Owner)), ent, args.User);
            return;
        }

        ToggleOpen(ent);
    }

    /// <summary>MouseDrop_T: посадить и закрыть дверь.</summary>
    private void OnTeleporterDragDrop(Entity<GulagTeleporterComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryInsert(ent, args.Dragged);
    }

    private void OnTeleporterVerbs(Entity<GulagTeleporterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (ent.Comp.Occupant.ContainedEntity != null && !ent.Comp.Locked)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("gulag-teleporter-verb-eject"),
                Category = VerbCategory.Eject,
                Priority = 1,
                Act = () => SetOpen(ent, true),
            });
        }
        else if (ent.Comp.Open && ent.Comp.Occupant.ContainedEntity == null && _blocker.CanMove(user))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("gulag-teleporter-verb-enter"),
                Act = () => TryInsert(ent, user),
            });
        }
    }

    public bool TryInsert(Entity<GulagTeleporterComponent> ent, EntityUid target)
    {
        if (!ent.Comp.Open || ent.Comp.Occupant.ContainedEntity != null || !HasComp<HumanoidProfileComponent>(target))
            return false;

        if (!_container.Insert(target, ent.Comp.Occupant))
            return false;

        ent.Comp.Open = false;
        Dirty(ent);
        UpdateTeleporterAppearance(ent);
        return true;
    }

    /// <summary>toggle_open().</summary>
    private void ToggleOpen(Entity<GulagTeleporterComponent> ent)
    {
        if (ent.Comp.Open)
            SetOpen(ent, false);
        else if (!ent.Comp.Locked)
            SetOpen(ent, true);
    }

    /// <summary>open_machine()/close_machine(): открытая дверь выпускает того, кто внутри.</summary>
    private void SetOpen(Entity<GulagTeleporterComponent> ent, bool open)
    {
        ent.Comp.Open = open;
        if (open && ent.Comp.Occupant.ContainedEntity is { } occupant)
            _container.Remove(occupant, ent.Comp.Occupant);

        Dirty(ent);
        UpdateTeleporterAppearance(ent);
    }

    /// <summary>relaymove() и container_resist_act().</summary>
    private void OnTeleporterRelayMove(Entity<GulagTeleporterComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        var user = args.Entity;
        if (ent.Comp.BreakingOut || !_blocker.CanInteract(user, ent))
            return;

        var restrained = TryComp<CuffableComponent>(user, out var cuffable) && !cuffable.CanStillInteract;
        if (!ent.Comp.Locked && !restrained)
        {
            SetOpen(ent, true);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextMessage)
            return;

        ent.Comp.NextMessage = _timing.CurTime + TimeSpan.FromSeconds(5);
        var time = ent.Comp.Locked ? ent.Comp.BreakoutTime : ent.Comp.BreakoutTime / 2;
        _popup.PopupEntity(Loc.GetString("gulag-teleporter-breakout-start", ("time", (int) time.TotalSeconds)), user, user);
        _popup.PopupEntity(Loc.GetString("gulag-teleporter-breakout-others", ("user", user), ("machine", ent.Owner)), ent, Robust.Shared.Player.Filter.PvsExcept(user), true);

        var doAfter = new DoAfterArgs(EntityManager, user, time, new GulagBreakoutDoAfterEvent(), ent, ent)
        {
            BreakOnMove = false,
            NeedHand = false,
        };

        ent.Comp.BreakingOut = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBreakout(Entity<GulagTeleporterComponent> ent, ref GulagBreakoutDoAfterEvent args)
    {
        ent.Comp.BreakingOut = false;
        if (args.Cancelled || args.Handled || ent.Comp.Open || ent.Comp.Occupant.ContainedEntity != args.User)
            return;

        args.Handled = true;
        ent.Comp.Locked = false;
        _popup.PopupEntity(Loc.GetString("gulag-teleporter-breakout-done", ("user", args.User), ("machine", ent.Owner)), ent, PopupType.MediumCaution);
        SetOpen(ent, true);
    }

    private void OnTeleporterContainerChanged(Entity<GulagTeleporterComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateTeleporterAppearance(ent);
    }

    private void OnTeleporterContainerChanged(Entity<GulagTeleporterComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == GulagTeleporterComponent.ContainerId)
            ent.Comp.BreakingOut = false;

        UpdateTeleporterAppearance(ent);
    }

    private void OnTeleporterShutdown(Entity<GulagTeleporterComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Occupant.ContainedEntity is { } occupant && !TerminatingOrDeleted(occupant))
            _container.Remove(occupant, ent.Comp.Occupant);
    }

    private void UpdateTeleporterAppearance(Entity<GulagTeleporterComponent> ent)
    {
        var state = ent.Comp.Open
            ? GulagTeleporterState.Open
            : ent.Comp.Occupant.ContainedEntity != null ? GulagTeleporterState.Occupied : GulagTeleporterState.Closed;
        _appearance.SetData(ent, GulagTeleporterVisuals.State, state);
    }

    #endregion

    #region Консоль

    private void OnConsoleMapInit(Entity<GulagConsoleComponent> ent, ref MapInitEvent args)
    {
        ScanMachinery(ent);
    }

    /// <summary>scan_machinery().</summary>
    private void ScanMachinery(Entity<GulagConsoleComponent> ent)
    {
        ent.Comp.Teleporter = FindTeleporter(ent);
        ent.Comp.Beacon = FindBeacon();
    }

    /// <summary>findteleporter(): работающий телепорт на соседней клетке.</summary>
    private EntityUid? FindTeleporter(EntityUid console)
    {
        var xform = Transform(console);
        var origin = _xform.GetWorldPosition(xform);
        foreach (var tele in _lookup.GetEntitiesInRange<GulagTeleporterComponent>(xform.Coordinates, 1.2f))
        {
            var delta = _xform.GetWorldPosition(tele) - origin;
            if (MathF.Abs(MathF.Abs(delta.X) + MathF.Abs(delta.Y) - 1f) > 0.3f)
                continue;

            if (_power.IsPowered(tele.Owner))
                return tele.Owner;
        }

        return null;
    }

    /// <summary>findbeacon().</summary>
    public EntityUid? FindBeacon()
    {
        var query = EntityQueryEnumerator<GulagBeaconComponent>();
        return query.MoveNext(out var uid, out _) ? uid : null;
    }

    private void OnConsoleUiOpened(Entity<GulagConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsoleUi(ent);
    }

    private void OnConsoleSlotChanged(Entity<GulagConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateConsoleUi(ent);
    }

    private void OnConsoleSlotChanged(Entity<GulagConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateConsoleUi(ent);
    }

    private void OnConsoleAction(Entity<GulagConsoleComponent> ent, ref GulagConsoleActionMessage args)
    {
        var user = args.Actor;
        _audio.PlayPvs(ConfirmSound, ent, AudioParams.Default.WithVolume(-4f));
        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("gulag-console-access-denied"), ent, user);
            return;
        }

        switch (args.Action)
        {
            case GulagConsoleAction.ScanTeleporter:
                ent.Comp.Teleporter = FindTeleporter(ent);
                break;
            case GulagConsoleAction.ScanBeacon:
                _campMap.EnsureCamp();
                ent.Comp.Beacon = FindBeacon();
                break;
            case GulagConsoleAction.HandleId:
                if (_itemSlots.TryGetSlot(ent, GulagConsoleComponent.IdSlot, out var slot))
                {
                    if (slot.HasItem)
                        _itemSlots.TryEjectToHands(ent, slot, user, true);
                    else
                        _itemSlots.TryInsertFromHand(ent, slot, user);
                }
                break;
            case GulagConsoleAction.ToggleOpen:
                if (GetTeleporter(ent) is not { } openTele)
                    break;
                if (openTele.Comp.Locked)
                {
                    _popup.PopupEntity(Loc.GetString("gulag-console-unlock-first"), ent, user);
                    break;
                }
                ToggleOpen(openTele);
                break;
            case GulagConsoleAction.ToggleLock:
                if (GetTeleporter(ent) is not { } lockTele)
                    break;
                if (lockTele.Comp.Open)
                {
                    _popup.PopupEntity(Loc.GetString("gulag-console-close-first"), ent, user);
                    break;
                }
                lockTele.Comp.Locked = !lockTele.Comp.Locked;
                Dirty(lockTele);
                break;
            case GulagConsoleAction.Teleport:
                Teleport(ent, user);
                break;
        }

        UpdateConsoleUi(ent);
    }

    /// <summary>set_goal: 0 — значение по умолчанию, максимум 1000.</summary>
    private void OnConsoleSetGoal(Entity<GulagConsoleComponent> ent, ref GulagConsoleSetGoalMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent) || GetId(ent) is not { } id)
            return;

        var goal = args.Goal == 0 ? ent.Comp.DefaultGoal : args.Goal;
        EnsureComp<GulagPrisonerIdComponent>(id).Goal = Math.Clamp(goal, 0, 1000);
        UpdateConsoleUi(ent);
    }

    private Entity<GulagTeleporterComponent>? GetTeleporter(Entity<GulagConsoleComponent> ent)
    {
        if (ent.Comp.Teleporter is not { } tele || TerminatingOrDeleted(tele) || !TryComp<GulagTeleporterComponent>(tele, out var comp))
            return null;

        return (tele, comp);
    }

    private EntityUid? GetId(EntityUid console)
    {
        return _itemSlots.GetItemOrNull(console, GulagConsoleComponent.IdSlot);
    }

    private void UpdateConsoleUi(Entity<GulagConsoleComponent> ent)
    {
        var state = new GulagConsoleState();
        var tele = GetTeleporter(ent);
        if (tele is { } t)
        {
            state.HasTeleporter = true;
            state.TeleporterLocation = FormatLocation(t);
            state.TeleporterLocked = t.Comp.Locked;
            state.TeleporterOpen = t.Comp.Open;

            if (t.Comp.Occupant.ContainedEntity is { } prisoner && HasComp<HumanoidProfileComponent>(prisoner))
            {
                state.PrisonerName = Name(prisoner);
                state.CrimeStatus = GetStatus(prisoner) is { } status
                    ? Loc.GetString($"criminal-records-status-{status.ToString().ToLowerInvariant()}")
                    : null;
            }
        }

        if (ent.Comp.Beacon is { } beacon && !TerminatingOrDeleted(beacon))
        {
            state.HasBeacon = true;
            state.BeaconLocation = FormatLocation(beacon);
        }

        if (GetId(ent) is { } id)
        {
            state.HasId = true;
            state.IdName = Name(id);
            state.Goal = CompOrNull<GulagPrisonerIdComponent>(id)?.Goal ?? 0;
            state.CanTeleport = state.PrisonerName != null;
        }

        _ui.SetUiState(ent.Owner, GulagConsoleUiKey.Key, state);
    }

    private string FormatLocation(EntityUid uid)
    {
        var pos = _xform.GetMapCoordinates(uid);
        var map = _xform.GetMap(uid) is { } mapUid ? Name(mapUid) : "?";
        return $"({(int) pos.Position.X}, {(int) pos.Position.Y}, {map})";
    }

    /// <summary>teleport(): раздеть, переодеть и отправить к маяку.</summary>
    private void Teleport(Entity<GulagConsoleComponent> ent, EntityUid user)
    {
        if (ent.Comp.Beacon == null || TerminatingOrDeleted(ent.Comp.Beacon.Value))
        {
            _campMap.EnsureCamp();
            ent.Comp.Beacon = FindBeacon();
        }

        if (GetTeleporter(ent) is not { } tele || ent.Comp.Beacon is not { } beacon || TerminatingOrDeleted(beacon))
            return;

        if (tele.Comp.Occupant.ContainedEntity is not { } prisoner || !HasComp<HumanoidProfileComponent>(prisoner))
            return;

        if (GetId(ent) is not { } id)
        {
            _popup.PopupEntity(Loc.GetString("gulag-console-no-id"), ent, PopupType.MediumCaution);
            return;
        }

        var idComp = EnsureComp<GulagPrisonerIdComponent>(id);
        if (idComp.Goal == 0)
        {
            idComp.Goal = ent.Comp.DefaultGoal;
            _popup.PopupEntity(Loc.GetString("gulag-console-default-goal", ("id", id), ("goal", idComp.Goal)), ent);
        }

        HandlePrisoner(tele, prisoner, id);

        _audio.PlayPvs(DepartSound, ent);
        _securityImplant.AllowNextMove(prisoner);
        _container.Remove(prisoner, tele.Comp.Occupant, destination: Transform(beacon).Coordinates);
        _stun.TryUpdateParalyzeDuration(prisoner, TimeSpan.FromSeconds(4));
        _popup.PopupEntity(Loc.GetString("gulag-teleport-dizzy"), prisoner, prisoner, PopupType.MediumCaution);
        Spawn("EffectSparks", Transform(prisoner).Coordinates);
        _audio.PlayPvs(ArriveSound, prisoner);

        tele.Comp.Locked = false;
        SetOpen(tele, true);
    }

    /// <summary>handle_prisoner(): раздеть, выдать робу, ботинки и ID, сменить статус.</summary>
    private void HandlePrisoner(Entity<GulagTeleporterComponent> tele, EntityUid prisoner, EntityUid id)
    {
        StripPrisoner(tele, prisoner);

        var coords = Transform(prisoner).Coordinates;
        EquipNew(prisoner, tele.Comp.JumpsuitProto, "jumpsuit", coords);
        EquipNew(prisoner, tele.Comp.ShoesProto, "shoes", coords);

        if (!_inventory.TryEquip(prisoner, id, "id", true, true) && !_hands.TryPickupAnyHand(prisoner, id, false))
            _xform.SetCoordinates(id, coords);

        SetStatus(prisoner, SecurityStatus.Detained);
    }

    private void EquipNew(EntityUid prisoner, string proto, string slot, EntityCoordinates coords)
    {
        var item = Spawn(proto, coords);
        if (!_inventory.TryEquip(prisoner, item, slot, true, true))
            Del(item);
    }

    /// <summary>strip_prisoner(): всё, кроме баллонов и дыхательных масок, уходит в станцию возврата снаряжения.</summary>
    private void StripPrisoner(Entity<GulagTeleporterComponent> tele, EntityUid prisoner)
    {
        var reclaimer = FindReclaimer(tele);
        var dropCoords = Transform(tele).Coordinates;
        var stored = new List<EntityUid>();

        // Наручники падают на пол.
        if (TryComp<CuffableComponent>(prisoner, out var cuffable))
        {
            foreach (var cuffs in cuffable.Container.ContainedEntities.ToList())
            {
                _cuffable.Uncuff(prisoner, null, cuffs, cuffable);
                if (!TerminatingOrDeleted(cuffs))
                    _xform.SetCoordinates(cuffs, dropCoords);
            }
        }

        foreach (var held in _hands.EnumerateHeld(prisoner).ToList())
        {
            if (IsRequired(held) || !_hands.TryDrop(prisoner, held, checkActionBlocker: false, doDropInteraction: false))
                continue;

            Store(held);
        }

        if (_inventory.TryGetSlots(prisoner, out var slots))
        {
            // Сначала карманы, ID и пояс — они зависят от комбинезона.
            foreach (var slot in slots.OrderBy(s => s.DependsOn == null))
            {
                if (!_inventory.TryGetSlotEntity(prisoner, slot.Name, out var item) || IsRequired(item.Value))
                    continue;

                if (!_inventory.TryUnequip(prisoner, slot.Name, true, true))
                    continue;

                Store(item.Value);
            }
        }

        if (reclaimer is { } r)
            r.Comp.Stored[prisoner] = stored;

        void Store(EntityUid item)
        {
            if (reclaimer is { } rec && _container.Insert(item, rec.Comp.Items))
            {
                stored.Add(item);
                return;
            }

            _xform.SetCoordinates(item, dropCoords);
        }
    }

    /// <summary>telegulag_required_items.</summary>
    private bool IsRequired(EntityUid item)
    {
        return HasComp<GasTankComponent>(item) || HasComp<BreathToolComponent>(item);
    }

    /// <summary>locate_reclaimer(): ближайшая станция возврата на той же карте.</summary>
    private Entity<GulagReclaimerComponent>? FindReclaimer(EntityUid tele)
    {
        var map = _xform.GetMapId(tele);
        var origin = _xform.GetWorldPosition(tele);
        Entity<GulagReclaimerComponent>? best = null;
        var bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<GulagReclaimerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != map)
                continue;

            var dist = (_xform.GetWorldPosition(xform) - origin).LengthSquared();
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            best = (uid, comp);
        }

        return best;
    }

    #endregion

    #region Уголовные записи

    public SecurityStatus? GetStatus(EntityUid mob)
    {
        return TryGetRecordKey(mob, out var key) && _records.TryGetRecord<CriminalRecord>(key, out var record)
            ? record.Status
            : null;
    }

    public void SetStatus(EntityUid mob, SecurityStatus status)
    {
        if (TryGetRecordKey(mob, out var key))
            _criminalRecords.TryChangeStatus(key, status, null);
    }

    private bool TryGetRecordKey(EntityUid mob, out StationRecordKey key)
    {
        var name = Name(mob);
        foreach (var station in _station.GetStations())
        {
            if (_records.GetRecordByName(station, name) is { } id)
            {
                key = new StationRecordKey(id, station);
                return true;
            }
        }

        key = default;
        return false;
    }

    #endregion
}
