using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Spawners.Components;
using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Emag.Systems;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Security;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// laborstacker.dm и gulag_item_reclaimer.dm: сбор листов в очки, консоль очков,
/// возвращение на станцию по выполнении нормы и выдача отобранного снаряжения.
/// </summary>
public sealed class LaborCampSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly GulagSystem _gulag = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SecurityImplantSystem _securityImplant = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier ArriveSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    private TimeSpan _nextUiUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LaborStackerComponent, InteractUsingEvent>(OnStackerInteract);

        SubscribeLocalEvent<LaborClaimConsoleComponent, MapInitEvent>(OnClaimMapInit);
        SubscribeLocalEvent<LaborClaimConsoleComponent, BoundUIOpenedEvent>(OnClaimUiOpened);
        SubscribeLocalEvent<LaborClaimConsoleComponent, LaborClaimActionMessage>(OnClaimAction);
        SubscribeLocalEvent<LaborClaimConsoleComponent, GotEmaggedEvent>(OnClaimEmagged);

        SubscribeLocalEvent<LaborPointsCheckerComponent, InteractUsingEvent>(OnCheckerInteract);

        SubscribeLocalEvent<GulagReclaimerComponent, ComponentInit>(OnReclaimerInit);
        SubscribeLocalEvent<GulagReclaimerComponent, BoundUIOpenedEvent>(OnReclaimerUiOpened);
        SubscribeLocalEvent<GulagReclaimerComponent, GulagReclaimerReleaseMessage>(OnReclaimerRelease);
        SubscribeLocalEvent<GulagReclaimerComponent, GotEmaggedEvent>(OnReclaimerEmagged);
        SubscribeLocalEvent<GulagReclaimerComponent, EntRemovedFromContainerMessage>(OnReclaimerItemRemoved);
        SubscribeLocalEvent<GulagReclaimerComponent, ComponentShutdown>(OnReclaimerShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUiUpdate)
            return;

        _nextUiUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        var claims = EntityQueryEnumerator<LaborClaimConsoleComponent>();
        while (claims.MoveNext(out var uid, out var claim))
        {
            if (_ui.IsUiOpen(uid, LaborClaimUiKey.Key))
                UpdateClaimUi((uid, claim));
        }

        var reclaimers = EntityQueryEnumerator<GulagReclaimerComponent>();
        while (reclaimers.MoveNext(out var uid, out var reclaimer))
        {
            if (_ui.IsUiOpen(uid, GulagReclaimerUiKey.Key))
                UpdateReclaimerUi((uid, reclaimer));
        }
    }

    #region Сборщик

    /// <summary>laborstacker/process_stack(): каждый засчитанный лист — 33 очка.</summary>
    private void OnStackerInteract(Entity<LaborStackerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<StackComponent>(args.Used, out var stack))
            return;

        if (!ent.Comp.AcceptedStacks.Contains(stack.StackTypeId))
        {
            _popup.PopupEntity(Loc.GetString("labor-stacker-rejected", ("item", args.Used)), ent, args.User);
            return;
        }

        args.Handled = true;
        ent.Comp.Points += ent.Comp.PointsPerSheet * stack.Count;
        ent.Comp.Stored[stack.StackTypeId] = ent.Comp.Stored.GetValueOrDefault(stack.StackTypeId) + stack.Count;
        _popup.PopupEntity(Loc.GetString("labor-stacker-accepted", ("count", stack.Count), ("points", ent.Comp.Points)), ent, args.User);
        QueueDel(args.Used);
    }

    #endregion

    #region Консоль очков

    /// <summary>locate_stacking_machine(): сборщик в пределах двух клеток.</summary>
    private void OnClaimMapInit(Entity<LaborClaimConsoleComponent> ent, ref MapInitEvent args)
    {
        LocateStacker(ent);
    }

    private Entity<LaborStackerComponent>? LocateStacker(Entity<LaborClaimConsoleComponent> ent)
    {
        if (ent.Comp.Stacker is { } known && !TerminatingOrDeleted(known) && TryComp<LaborStackerComponent>(known, out var knownComp))
            return (known, knownComp);

        foreach (var stacker in _lookup.GetEntitiesInRange<LaborStackerComponent>(Transform(ent).Coordinates, 2.5f))
        {
            ent.Comp.Stacker = stacker;
            return stacker;
        }

        return null;
    }

    private void OnClaimUiOpened(Entity<LaborClaimConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateClaimUi(ent);
    }

    private void UpdateClaimUi(Entity<LaborClaimConsoleComponent> ent)
    {
        foreach (var actor in _ui.GetActors(ent.Owner, LaborClaimUiKey.Key))
        {
            _ui.SetUiState(ent.Owner, LaborClaimUiKey.Key, BuildClaimState(ent, actor));
            break;
        }
    }

    private LaborClaimState BuildClaimState(Entity<LaborClaimConsoleComponent> ent, EntityUid user)
    {
        var state = new LaborClaimState { CanGoHome = ent.Comp.Emagged };

        if (GetPrisonerId(user) is { } id)
        {
            state.IdPoints = id.Comp.Points;
            if (id.Comp.Goal == 0)
            {
                state.StatusInfo = Loc.GetString("labor-claim-no-goal");
            }
            else if (id.Comp.Points >= id.Comp.Goal)
            {
                state.CanGoHome = true;
                state.StatusInfo = Loc.GetString("labor-claim-goal-met");
            }
            else
            {
                state.StatusInfo = Loc.GetString("labor-claim-points-away", ("points", id.Comp.Goal - id.Comp.Points));
            }
        }
        else
        {
            state.StatusInfo = Loc.GetString("labor-claim-no-id");
        }

        if (LocateStacker(ent) is { } stacker)
            state.UnclaimedPoints = stacker.Comp.Points;

        return state;
    }

    /// <summary>get_idcard(TRUE): карта в руках или в слоте ID.</summary>
    private Entity<GulagPrisonerIdComponent>? GetPrisonerId(EntityUid user)
    {
        if (!_idCard.TryFindIdCard(user, out var card))
            return null;

        return (card.Owner, EnsureComp<GulagPrisonerIdComponent>(card));
    }

    private void OnClaimAction(Entity<LaborClaimConsoleComponent> ent, ref LaborClaimActionMessage args)
    {
        var user = args.Actor;
        switch (args.Action)
        {
            case LaborClaimAction.ClaimPoints:
                if (GetPrisonerId(user) is { } id && LocateStacker(ent) is { } stacker)
                {
                    id.Comp.Points += stacker.Comp.Points;
                    stacker.Comp.Points = 0;
                    Say(ent, Loc.GetString("labor-claim-transferred"), force: true);
                }
                else
                {
                    Say(ent, Loc.GetString("labor-claim-no-valid-id"));
                }
                break;

            case LaborClaimAction.MoveShuttle:
                if (!BuildClaimState(ent, user).CanGoHome)
                    break;

                Release(ent, user);
                break;
        }

        UpdateClaimUi(ent);
    }

    /// <summary>
    /// move_shuttle: в SS13 заключённый улетает шаттлом трудового лагеря. Шаттла в сборке нет,
    /// поэтому освобождённого вместе с добытыми листами переносит к станции возврата снаряжения.
    /// </summary>
    private void Release(Entity<LaborClaimConsoleComponent> ent, EntityUid user)
    {
        if (FindReturnPoint(ent) is not { } dest)
        {
            Say(ent, Loc.GetString("labor-claim-no-shuttle"));
            return;
        }

        if (!ent.Comp.Emagged)
        {
            _gulag.SetStatus(user, SecurityStatus.Paroled);
            _radio.SendRadioMessage(ent, Loc.GetString("labor-claim-announce", ("person", Name(user))), "Security", ent);
        }

        Say(ent, Loc.GetString("labor-claim-finished"), force: true);
        Spawn("EffectSparks", Transform(user).Coordinates);
        _securityImplant.AllowNextMove(user);
        _xform.SetCoordinates(user, dest);
        _xform.AttachToGridOrMap(user);
        _audio.PlayPvs(ArriveSound, user);

        if (LocateStacker(ent) is { } stacker)
        {
            foreach (var (type, count) in stacker.Comp.Stored)
            {
                _stack.SpawnMultipleAtPosition(new ProtoId<StackPrototype>(type), count, dest);
            }

            stacker.Comp.Stored.Clear();
        }
    }

    /// <summary>laborcamp_home: станция возврата снаряжения или телепорт гулага на другой карте.</summary>
    private EntityCoordinates? FindReturnPoint(EntityUid console)
    {
        var campMap = _xform.GetMapId(console);

        var reclaimers = EntityQueryEnumerator<GulagReclaimerComponent, TransformComponent>();
        while (reclaimers.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != campMap)
                return xform.Coordinates;
        }

        var teleporters = EntityQueryEnumerator<GulagTeleporterComponent, TransformComponent>();
        while (teleporters.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != campMap)
                return xform.Coordinates;
        }

        var spawns = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (spawns.MoveNext(out _, out var spawn, out var xform))
        {
            if (spawn.SpawnType == SpawnPointType.LateJoin && xform.MapID != campMap)
                return xform.Coordinates;
        }

        return null;
    }

    private void OnClaimEmagged(Entity<LaborClaimConsoleComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("labor-claim-emagged", ("machine", ent.Owner)), ent, PopupType.MediumCaution);
        Spawn("EffectSparks", Transform(ent).Coordinates);
    }

    /// <summary>say() с кулдауном 2 секунды на повторы.</summary>
    private void Say(Entity<LaborClaimConsoleComponent> ent, string message, bool force = false)
    {
        if (!force && _timing.CurTime < ent.Comp.NextSay)
            return;

        ent.Comp.NextSay = _timing.CurTime + TimeSpan.FromSeconds(2);
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    #endregion

    #region Проверка очков

    /// <summary>labor_points_checker/attackby(): провести ID заключённого.</summary>
    private void OnCheckerInteract(Entity<LaborPointsCheckerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<IdCardComponent>(args.Used, out var card))
            return;

        args.Handled = true;
        var id = CompOrNull<GulagPrisonerIdComponent>(args.Used);
        if (id == null || id.Goal == 0)
        {
            CheckerSay(ent, Loc.GetString("labor-checker-no-goal"));
            return;
        }

        CheckerSay(ent, Loc.GetString("labor-checker-id", ("name", card.FullName ?? Name(args.Used))));
        CheckerSay(ent, Loc.GetString("labor-checker-points", ("points", id.Points), ("goal", id.Goal)));
        CheckerSay(ent, Loc.GetString("labor-checker-info"));
    }

    private void CheckerSay(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, ChatTransmitRange.Normal, ignoreActionBlocker: true);
    }

    #endregion

    #region Станция возврата снаряжения

    private void OnReclaimerInit(Entity<GulagReclaimerComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Items = _container.EnsureContainer<Container>(ent, GulagReclaimerComponent.ContainerId);
    }

    private void OnReclaimerUiOpened(Entity<GulagReclaimerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateReclaimerUi(ent);
    }

    private void UpdateReclaimerUi(Entity<GulagReclaimerComponent> ent)
    {
        foreach (var actor in _ui.GetActors(ent.Owner, GulagReclaimerUiKey.Key))
        {
            _ui.SetUiState(ent.Owner, GulagReclaimerUiKey.Key, BuildReclaimerState(ent, actor));
            break;
        }
    }

    private GulagReclaimerState BuildReclaimerState(Entity<GulagReclaimerComponent> ent, EntityUid user)
    {
        var state = new GulagReclaimerState { CanReclaim = CanReclaim(ent, user) };

        foreach (var mob in ent.Comp.Stored.Keys.ToList())
        {
            if (TerminatingOrDeleted(mob))
            {
                _chat.TrySendInGameICMessage(ent, Loc.GetString("gulag-reclaimer-lost-signal"), InGameICChatType.Speak,
                    ChatTransmitRange.Normal, ignoreActionBlocker: true);
                DropItems(ent, mob);
                continue;
            }

            state.Mobs.Add(GetNetEntity(mob));
            state.Names.Add(Name(mob));
        }

        return state;
    }

    /// <summary>allowed(user) или выполненная норма на ID заключённого.</summary>
    private bool CanReclaim(Entity<GulagReclaimerComponent> ent, EntityUid user)
    {
        if (ent.Comp.Emagged || _access.IsAllowed(user, ent))
            return true;

        return GetPrisonerId(user) is { } id && id.Comp.Points >= id.Comp.Goal;
    }

    private void OnReclaimerRelease(Entity<GulagReclaimerComponent> ent, ref GulagReclaimerReleaseMessage args)
    {
        var user = args.Actor;
        var mob = GetEntity(args.Mob);
        if (!ent.Comp.Stored.ContainsKey(mob))
            return;

        if (mob != user && !ent.Comp.Emagged && !_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("gulag-console-access-denied"), ent, user);
            return;
        }

        if (mob == user && !CanReclaim(ent, user))
            return;

        DropItems(ent, mob);
        UpdateReclaimerUi(ent);
    }

    /// <summary>drop_items().</summary>
    private void DropItems(Entity<GulagReclaimerComponent> ent, EntityUid mob)
    {
        if (!ent.Comp.Stored.Remove(mob, out var items))
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var item in items)
        {
            if (!TerminatingOrDeleted(item) && ent.Comp.Items.Contains(item))
                _container.Remove(item, ent.Comp.Items, destination: coords);
        }
    }

    private void OnReclaimerItemRemoved(Entity<GulagReclaimerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        foreach (var list in ent.Comp.Stored.Values)
        {
            list.Remove(args.Entity);
        }
    }

    private void OnReclaimerEmagged(Entity<GulagReclaimerComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        args.Handled = true;
        _appearance.SetData(ent, GulagReclaimerVisuals.Emagged, true);
        _popup.PopupEntity(Loc.GetString("gulag-reclaimer-emagged"), ent, args.UserUid);
    }

    private void OnReclaimerShutdown(Entity<GulagReclaimerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var mob in ent.Comp.Stored.Keys.ToList())
        {
            DropItems(ent, mob);
        }
    }

    #endregion
}
