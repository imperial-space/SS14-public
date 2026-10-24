using Content.Server.Chat.Managers;
using Content.Server.Pinpointer;
using Content.Shared.Access.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Security;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// computer/prisoner/management и implant/chem: ID заключённых, химимпланты и трекинг-импланты.
/// </summary>
public sealed class PrisonerManagementSystem : EntitySystem
{
    /// <summary>implant/tracking — ванильный трекинг-имплант.</summary>
    private const string TrackingImplantProto = "TrackingImplant";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SecurityImplantSystem _securityImplant = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrisonerManagementConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PrisonerManagementConsoleComponent, PrisonerManagementActionMessage>(OnAction);
        SubscribeLocalEvent<PrisonerManagementConsoleComponent, PrisonerManagementSetGoalMessage>(OnSetGoal);
        SubscribeLocalEvent<PrisonerManagementConsoleComponent, PrisonerImplantActionMessage>(OnImplantAction);

        SubscribeLocalEvent<ChemImplantComponent, EntGotRemovedFromContainerMessage>(OnChemImplantLeftImplanter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        // on_death(): химимплант выпускает всё содержимое при смерти носителя.
        var implants = EntityQueryEnumerator<ChemImplantComponent, SubdermalImplantComponent>();
        while (implants.MoveNext(out var uid, out var chem, out var implant))
        {
            if (chem.DeathTriggered || implant.ImplantedEntity is not { } mob || !_mobState.IsDead(mob))
                continue;

            chem.DeathTriggered = true;
            Inject((uid, chem), mob, null);
        }

        var consoles = EntityQueryEnumerator<PrisonerManagementConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, PrisonerManagementUiKey.Key))
                UpdateUi(uid);
        }
    }

    #region Консоль

    private void OnUiOpened(Entity<PrisonerManagementConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private bool Authorized(EntityUid console, EntityUid user)
    {
        if (_access.IsAllowed(user, console))
            return true;

        _popup.PopupEntity(Loc.GetString("gulag-console-access-denied"), console, user);
        return false;
    }

    private void OnAction(Entity<PrisonerManagementConsoleComponent> ent, ref PrisonerManagementActionMessage args)
    {
        _audio.PlayPvs(ConfirmSound, ent, AudioParams.Default.WithVolume(-4f));
        if (!Authorized(ent, args.Actor) || !_itemSlots.TryGetSlot(ent, PrisonerManagementConsoleComponent.IdSlot, out var slot))
            return;

        switch (args.Action)
        {
            case PrisonerManagementAction.InsertId:
                _itemSlots.TryInsertFromHand(ent, slot, args.Actor);
                break;
            case PrisonerManagementAction.EjectId:
                _itemSlots.TryEjectToHands(ent, slot, args.Actor, true);
                break;
            case PrisonerManagementAction.ResetId:
                if (slot.Item is { } id)
                    EnsureComp<GulagPrisonerIdComponent>(id).Points = 0;
                break;
        }

        UpdateUi(ent);
    }

    /// <summary>set_id_goal: от 1 до 1000.</summary>
    private void OnSetGoal(Entity<PrisonerManagementConsoleComponent> ent, ref PrisonerManagementSetGoalMessage args)
    {
        if (!Authorized(ent, args.Actor) || _itemSlots.GetItemOrNull(ent, PrisonerManagementConsoleComponent.IdSlot) is not { } id)
            return;

        EnsureComp<GulagPrisonerIdComponent>(id).Goal = Math.Clamp(args.Goal, 1, 1000);
        UpdateUi(ent);
    }

    private void OnImplantAction(Entity<PrisonerManagementConsoleComponent> ent, ref PrisonerImplantActionMessage args)
    {
        _audio.PlayPvs(ConfirmSound, ent, AudioParams.Default.WithVolume(-4f));
        if (!Authorized(ent, args.Actor))
            return;

        var implant = GetEntity(args.Implant);
        if (!TryComp<SubdermalImplantComponent>(implant, out var sub) || sub.ImplantedEntity is not { } mob || !IsShown(ent, implant, sub))
            return;

        switch (args.Action)
        {
            // Self Destruct: имплант безвредно разрушается изнутри.
            case PrisonerImplantAction.SelfDestruct:
                Message(mob, Loc.GetString("prisoner-implant-self-destruct"));
                Spawn("EffectSparks", Transform(mob).Coordinates);
                QueueDel(implant);
                break;

            case PrisonerImplantAction.Inject:
                if (TryComp<ChemImplantComponent>(implant, out var chem) && chem.Sizes.Contains(args.Amount))
                    Inject((implant, chem), mob, args.Amount);
                break;

            // Warn: сообщение прямо в мозг.
            case PrisonerImplantAction.Warn:
                if (IsTracking(implant) && !string.IsNullOrWhiteSpace(args.Text))
                {
                    var text = args.Text.Trim();
                    if (text.Length > 256)
                        text = text[..256];
                    Message(mob, Loc.GetString("prisoner-implant-warn", ("message", text)));
                }
                break;
        }

        UpdateUi(ent);
    }

    private void UpdateUi(EntityUid console)
    {
        var state = new PrisonerManagementState();
        if (_itemSlots.GetItemOrNull(console, PrisonerManagementConsoleComponent.IdSlot) is { } id)
        {
            state.HasId = true;
            state.IdName = Name(id);
            var comp = CompOrNull<GulagPrisonerIdComponent>(id);
            state.Points = comp?.Points ?? 0;
            state.Goal = comp?.Goal ?? 0;
        }

        var query = EntityQueryEnumerator<SubdermalImplantComponent>();
        while (query.MoveNext(out var implant, out var sub))
        {
            if (sub.ImplantedEntity is not { } mob || !IsShown(console, implant, sub))
                continue;

            var info = new PrisonerImplantInfo
            {
                Implant = GetNetEntity(implant),
                Category = Name(implant),
            };
            info.InfoKeys.Add(Loc.GetString("prisoner-implant-info-id"));
            info.InfoValues.Add(Name(mob));

            if (HasComp<BeaconImplantComponent>(implant))
            {
                info.InfoKeys.Add(Loc.GetString("prisoner-implant-info-status"));
                info.InfoValues.Add(_securityImplant.GetBeaconStatus(mob));
            }
            else if (TryComp<ChemImplantComponent>(implant, out _))
            {
                info.CanInject = true;
                info.InfoKeys.Add(Loc.GetString("prisoner-implant-info-volume"));
                info.InfoValues.Add($"{GetVolume(implant)}u");
            }
            else
            {
                info.CanWarn = true;
                info.InfoKeys.Add(Loc.GetString("prisoner-implant-info-location"));
                info.InfoValues.Add(_navMap.GetNearestBeaconString(mob, true));
            }

            state.Implants.Add(info);
        }

        _ui.SetUiState(console, PrisonerManagementUiKey.Key, state);
    }

    /// <summary>
    /// is_shown_on_console(): химимпланты и трекинг-импланты на той же карте, что и консоль.
    /// Трекинг-имплант гаснет через 10 минут после смерти носителя (в сборке — сразу после смерти).
    /// </summary>
    private bool IsShown(EntityUid console, EntityUid implant, SubdermalImplantComponent sub)
    {
        if (sub.ImplantedEntity is not { } mob)
            return false;

        // beacon/is_shown_on_console(): маяк виден с любой карты.
        if (TryComp<BeaconImplantComponent>(implant, out var beacon))
            return _securityImplant.IsBeaconShown((implant, beacon));

        if (_xform.GetMapId(mob) != _xform.GetMapId(console))
            return false;

        if (HasComp<ChemImplantComponent>(implant))
            return true;

        return IsTracking(implant) && !_mobState.IsDead(mob);
    }

    private bool IsTracking(EntityUid implant)
    {
        return MetaData(implant).EntityPrototype?.ID == TrackingImplantProto;
    }

    private void Message(EntityUid mob, string text)
    {
        if (!_players.TryGetSessionByEntity(mob, out var session))
            return;

        _chatManager.ChatMessageToOne(ChatChannel.Local, text, text, default, false, session.Channel);
    }

    #endregion

    #region Химимплант

    /// <summary>
    /// Имплантер наполняется шприцем, а при вживлении раствор переливается в сам имплант
    /// (в SS13 наполняли футляр импланта).
    /// </summary>
    private void OnChemImplantLeftImplanter(Entity<ChemImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ImplanterComponent.ImplanterSlotId)
            return;

        var implanter = args.Container.Owner;
        if (!_solution.TryGetSolution(implanter, ent.Comp.ImplanterSolution, out var from, out var fromSolution) ||
            fromSolution.Volume <= 0 ||
            !_solution.TryGetSolution(ent.Owner, ChemImplantComponent.SolutionName, out var to, out _))
        {
            return;
        }

        var moved = _solution.SplitSolution(from.Value, fromSolution.Volume);
        _solution.TryAddSolution(to.Value, moved);
    }

    private float GetVolume(EntityUid implant)
    {
        return _solution.TryGetSolution(implant, ChemImplantComponent.SolutionName, out _, out var solution)
            ? solution.Volume.Float()
            : 0f;
    }

    /// <summary>activate(): ввести дозу в кровь; опустевший имплант рассасывается.</summary>
    private void Inject(Entity<ChemImplantComponent> ent, EntityUid mob, int? amount)
    {
        if (!_solution.TryGetSolution(ent.Owner, ChemImplantComponent.SolutionName, out var soln, out var solution))
            return;

        var quantity = amount ?? solution.Volume.Float();
        if (quantity > 0)
        {
            var dose = _solution.SplitSolution(soln.Value, quantity);
            _bloodstream.TryAddToBloodstream(mob, dose);
        }

        Message(mob, Loc.GetString("prisoner-chem-implant-beep"));
        if (solution.Volume > 0)
            return;

        Message(mob, Loc.GetString("prisoner-chem-implant-empty"));
        QueueDel(ent);
    }

    #endregion
}
