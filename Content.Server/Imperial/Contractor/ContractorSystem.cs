using System.Linq;
using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Drunk;
using Content.Shared.Flash;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Contractor;
using Content.Shared.Imperial.Contractor.Components;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Content.Shared.StatusEffectNew;
using Content.Shared.Storage.Components;
using Content.Shared.Body.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Contractor;

/// <summary>
/// Контрактник Синдиката 1 в 1 с SS13 (code/modules/antagonists/traitor/contractor,
/// modular_computers/.../contractor_program.dm): хаб контрактов, капсула эвакуации, выкуп и возврат жертв.
/// </summary>
public sealed class ContractorSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PinpointerSystem _pinpointer = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId ContractorRole = "MindRoleContractor";
    private static readonly EntProtoId ExtractionPod = "ContractorExtractionPod";
    private static readonly EntProtoId PodLanding = "ContractorExtractionPodLanding";
    private static readonly EntProtoId PinpointerMarker = "ContractorPinpointerMarker";
    private static readonly EntProtoId Telecrystal = "Telecrystal1";
    private static readonly EntProtoId Cash = "SpaceCash";
    private static readonly ProtoId<Content.Shared.Dataset.DatasetPrototype> WantedBase = "ContractorWantedBase";
    private static readonly ProtoId<Content.Shared.Dataset.DatasetPrototype> WantedVerb = "ContractorWantedVerb";
    private static readonly ProtoId<Content.Shared.Random.WeightedRandomPrototype> WantedNoun = "ContractorWantedNoun";
    private static readonly ProtoId<Content.Shared.Random.WeightedRandomPrototype> WantedLocation = "ContractorWantedLocation";
    private static readonly ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Omnizine = "Omnizine";
    private const string HoldingMapPath = "/Maps/Shuttles/contractor_prison.yml";
    private const string BelongingsContainer = "contractor_belongings";

    private static readonly SoundSpecifier StartupSound = new SoundPathSpecifier("/Audio/Imperial/Contractor/contractstartup.ogg");
    private static readonly SoundSpecifier DropoffSound = new SoundPathSpecifier("/Audio/Imperial/Contractor/confirmdropoff.ogg");
    private static readonly SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Imperial/Contractor/uplinkerror.ogg");
    private static readonly SoundSpecifier PodWoosh = new SoundPathSpecifier("/Audio/Imperial/Contractor/podwoosh.ogg");

    /// <summary>COME_BACK_FROM_CAPTURE_TIME.</summary>
    private static readonly TimeSpan CaptureTime = TimeSpan.FromMinutes(6);

    /// <summary>delays POD_TRANSIT + POD_FALLING.</summary>
    private static readonly TimeSpan PodArrival = TimeSpan.FromSeconds(2.9);

    /// <summary>lowest_TC_threshold.</summary>
    private const int LowestTcThreshold = 30;

    /// <summary>Радиус «зоны» вокруг маяка: дальше — уже другая зона.</summary>
    private const float AreaRadius = 14f;

    private static readonly TimeSpan PinpointerRefreshDelay = TimeSpan.FromSeconds(2.5);

    private readonly HashSet<EntityUid> _kidnapped = new();
    private readonly List<(TimeSpan At, Action Act)> _scheduled = new();
    private EntityUid? _holdingGrid;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContractorUplinkComponent, BoundUIOpenedEvent>(OnUplinkOpened);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorLoginMessage>(OnLogin);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorFirstLoadFinishedMessage>(OnFirstLoadFinished);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorToggleInfoMessage>(OnToggleInfo);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorClearErrorMessage>(OnClearError);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorAcceptContractMessage>(OnAccept);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorAbortContractMessage>(OnAbort);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorCallExtractionMessage>(OnCallExtraction);
        SubscribeLocalEvent<ContractorUplinkComponent, ContractorRedeemTcMessage>(OnRedeem);
        SubscribeLocalEvent<ContractorExtractionPodComponent, EntInsertedIntoContainerMessage>(OnPodEntered);
        SubscribeLocalEvent<ContractorPinpointerComponent, GotEquippedHandEvent>(OnPinpointerPickedUp);
        SubscribeLocalEvent<MindContainerComponent, ContractorRerollEvent>(OnReroll);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _kidnapped.Clear();
            _scheduled.Clear();
            _holdingGrid = null;
        });
    }

    #region Хаб

    private ContractorHubComponent? Hub(EntityUid? mindId)
    {
        return mindId is { } id && TryComp<ContractorHubComponent>(id, out var hub) ? hub : null;
    }

    public bool HasHub(EntityUid mindId)
    {
        return HasComp<ContractorHubComponent>(mindId);
    }

    /// <summary>contractor_hub/create_contracts().</summary>
    private void CreateContracts(EntityUid mindId, ContractorHubComponent hub)
    {
        var toGenerate = new List<ContractorPayoutType>
        {
            ContractorPayoutType.Large, ContractorPayoutType.Medium,
            ContractorPayoutType.Small, ContractorPayoutType.Small, ContractorPayoutType.Small, ContractorPayoutType.Small,
        };

        var candidates = PossibleTargets(mindId, hub.AssignedTargets).Count;
        if (toGenerate.Count > candidates)
            toGenerate.RemoveRange(0, toGenerate.Count - candidates);

        _random.Shuffle(toGenerate);
        var startIndex = hub.Contracts.Count + 1;
        var total = 0;
        SyndicateContract? lowest = null;
        var lowestSum = 0;

        foreach (var type in toGenerate)
        {
            var contract = new SyndicateContract { PayoutType = type, Id = startIndex++ };
            Generate(mindId, contract, hub.AssignedTargets);
            if (contract.TargetMind is { } target)
                hub.AssignedTargets.Add(target);

            var sum = contract.Payout + contract.PayoutBonus;
            if (lowest == null || sum < lowestSum)
            {
                lowestSum = sum;
                lowest = contract;
            }

            total += sum;
            hub.Contracts.Add(contract);
        }

        if (total < LowestTcThreshold && lowest != null)
            lowest.PayoutBonus += LowestTcThreshold - total;
    }

    /// <summary>syndicate_contract/generate().</summary>
    private void Generate(EntityUid ownerMind, SyndicateContract contract, HashSet<EntityUid> blacklist)
    {
        var targets = PossibleTargets(ownerMind, blacklist);
        contract.TargetMind = targets.Count > 0 ? _random.Pick(targets) : null;

        contract.TargetName = string.Empty;
        contract.TargetRank = Loc.GetString("contractor-rank-unknown");
        if (contract.TargetMind is { } targetMind && TryComp<MindComponent>(targetMind, out var mind))
        {
            contract.TargetName = mind.CharacterName ?? (mind.OwnedEntity is { } body ? Name(body) : string.Empty);
            if (_jobs.MindTryGetJobName(targetMind, out var job))
                contract.TargetRank = job;
        }

        contract.PayoutBonus = contract.PayoutType switch
        {
            ContractorPayoutType.Large => _random.Next(9, 14),
            ContractorPayoutType.Medium => _random.Next(6, 9),
            _ => _random.Next(2, 5),
        };
        contract.Payout = _random.Next(0, 3);

        GenerateDropoff(ownerMind, contract);
        contract.Ransom = 100 * _random.Next(18, 46);

        var noun = _proto.Index(WantedNoun).Pick(_random);
        var location = _proto.Index(WantedLocation).Pick(_random);
        contract.WantedMessage = Loc.GetString("contractor-wanted-message",
            ("base", Loc.GetString(_random.Pick(_proto.Index(WantedBase).Values))),
            ("verb", Loc.GetString(_random.Pick(_proto.Index(WantedVerb).Values))),
            ("noun", Loc.GetString(noun)),
            ("location", Loc.GetString(location)));
    }

    /// <summary>objective/contract/is_valid_target(): живые члены экипажа станции, не похищенные ранее.</summary>
    private List<EntityUid> PossibleTargets(EntityUid ownerMind, HashSet<EntityUid> blacklist)
    {
        var owner = CompOrNull<MindComponent>(ownerMind)?.OwnedEntity;
        var station = owner != null ? _station.GetOwningStation(owner.Value) : null;
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out _, out var mindContainer))
        {
            if (mindContainer.Mind is not { } mindId || mindId == ownerMind || blacklist.Contains(mindId) || _kidnapped.Contains(mindId))
                continue;
            if (!HasComp<ActorComponent>(uid) || _mobState.IsDead(uid))
                continue;
            if (station != null && _station.GetOwningStation(uid) != station)
                continue;
            result.Add(mindId);
        }

        return result;
    }

    /// <summary>objective/contract/generate_dropoff(): случайная зона станции.</summary>
    private void GenerateDropoff(EntityUid ownerMind, SyndicateContract contract)
    {
        var owner = CompOrNull<MindComponent>(ownerMind)?.OwnedEntity;
        var station = owner != null ? _station.GetOwningStation(owner.Value) : null;
        var beacons = new List<(EntityUid, string)>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent>();
        while (query.MoveNext(out var uid, out var beacon))
        {
            if (string.IsNullOrWhiteSpace(beacon.Text))
                continue;
            if (station != null && _station.GetOwningStation(uid) != station)
                continue;
            beacons.Add((uid, beacon.Text));
        }

        if (beacons.Count == 0)
        {
            contract.Dropoff = null;
            contract.DropoffName = Loc.GetString("contractor-rank-unknown");
            return;
        }

        var (dropoff, name) = _random.Pick(beacons);
        contract.Dropoff = dropoff;
        contract.DropoffName = name;
    }

    /// <summary>Ближайший маяк станции к точке — «зона», в которой она находится.</summary>
    private bool InArea(EntityUid entity, EntityUid? dropoff)
    {
        if (dropoff is not { } beacon || TerminatingOrDeleted(beacon) || TerminatingOrDeleted(entity))
            return false;

        var pos = _transform.GetMapCoordinates(entity);
        var beaconPos = _transform.GetMapCoordinates(beacon);
        if (pos.MapId != beaconPos.MapId || (pos.Position - beaconPos.Position).Length() > AreaRadius)
            return false;

        var best = beacon;
        var bestDist = (pos.Position - beaconPos.Position).Length();
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (string.IsNullOrWhiteSpace(comp.Text) || xform.MapID != pos.MapId)
                continue;
            var dist = (_transform.GetWorldPosition(xform) - pos.Position).Length();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = uid;
            }
        }

        return best == beacon;
    }

    private EntityUid? TargetBody(SyndicateContract contract)
    {
        return contract.TargetMind is { } mind ? CompOrNull<MindComponent>(mind)?.OwnedEntity : null;
    }

    #endregion

    #region Аплинк

    private ContractorUplinkStateComponent UplinkState(EntityUid uplink)
    {
        return EnsureComp<ContractorUplinkStateComponent>(uplink);
    }

    private void OnUplinkOpened(Entity<ContractorUplinkComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    /// <summary>PRG_login: только предатель; при первом входе создаётся хаб.</summary>
    private void OnLogin(Entity<ContractorUplinkComponent> ent, ref ContractorLoginMessage args)
    {
        var state = UplinkState(ent);
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _) || !_roles.MindHasRole<TraitorRoleComponent>(mindId))
        {
            state.Error = Loc.GetString("contractor-error-unauthorized");
            UpdateUi(ent, args.Actor);
            return;
        }

        state.LoggedInMind = mindId;
        if (!HasComp<ContractorHubComponent>(mindId))
        {
            var hub = AddComp<ContractorHubComponent>(mindId);
            CreateContracts(mindId, hub);
            if (!_roles.MindHasRole<ContractorRoleComponent>(mindId))
                _roles.MindAddRole(mindId, ContractorRole, silent: true);
            _audio.PlayEntity(StartupSound, args.Actor, args.Actor);
        }

        UpdateUi(ent, args.Actor);
    }

    private void OnFirstLoadFinished(Entity<ContractorUplinkComponent> ent, ref ContractorFirstLoadFinishedMessage args)
    {
        UplinkState(ent).FirstLoad = false;
        UpdateUi(ent, args.Actor);
    }

    private void OnToggleInfo(Entity<ContractorUplinkComponent> ent, ref ContractorToggleInfoMessage args)
    {
        var state = UplinkState(ent);
        state.InfoScreen = !state.InfoScreen;
        UpdateUi(ent, args.Actor);
    }

    private void OnClearError(Entity<ContractorUplinkComponent> ent, ref ContractorClearErrorMessage args)
    {
        UplinkState(ent).Error = string.Empty;
        UpdateUi(ent, args.Actor);
    }

    /// <summary>PRG_contract-accept.</summary>
    private void OnAccept(Entity<ContractorUplinkComponent> ent, ref ContractorAcceptContractMessage args)
    {
        if (Hub(UplinkState(ent).LoggedInMind) is not { } hub || hub.CurrentContract != null)
            return;
        var contractId = args.ContractId;
        var contract = hub.Contracts.FirstOrDefault(c => c.Id == contractId);
        if (contract == null || contract.Status != ContractorContractStatus.Inactive)
            return;

        contract.Status = ContractorContractStatus.Active;
        hub.CurrentContract = contract;
        UpdateUi(ent, args.Actor);
    }

    /// <summary>PRG_contract_abort.</summary>
    private void OnAbort(Entity<ContractorUplinkComponent> ent, ref ContractorAbortContractMessage args)
    {
        if (Hub(UplinkState(ent).LoggedInMind) is not { CurrentContract: { } contract } hub)
            return;

        hub.CurrentContract = null;
        contract.Status = ContractorContractStatus.Aborted;
        UpdateUi(ent, args.Actor);
    }

    /// <summary>PRG_call_extraction.</summary>
    private void OnCallExtraction(Entity<ContractorUplinkComponent> ent, ref ContractorCallExtractionMessage args)
    {
        var state = UplinkState(ent);
        if (state.LoggedInMind is not { } mindId || Hub(mindId) is not { CurrentContract: { } contract })
            return;

        if (contract.Status == ContractorContractStatus.Extracting)
        {
            _audio.PlayEntity(ErrorSound, args.Actor, args.Actor);
            state.Error = Loc.GetString("contractor-error-already-extracting");
        }
        else if (HandleExtraction(mindId, contract, args.Actor))
        {
            _audio.PlayEntity(DropoffSound, args.Actor, args.Actor);
            contract.Status = ContractorContractStatus.Extracting;
        }
        else
        {
            _audio.PlayEntity(ErrorSound, args.Actor, args.Actor);
            state.Error = Loc.GetString("contractor-error-dropoff");
        }

        UpdateUi(ent, args.Actor);
    }

    /// <summary>PRG_redeem_TC: телекристаллы материализуются в руках.</summary>
    private void OnRedeem(Entity<ContractorUplinkComponent> ent, ref ContractorRedeemTcMessage args)
    {
        if (Hub(UplinkState(ent).LoggedInMind) is not { } hub)
            return;

        if (hub.TcToRedeem <= 0)
        {
            _audio.PlayEntity(ErrorSound, args.Actor, args.Actor);
            return;
        }

        var crystals = Spawn(Telecrystal, _transform.GetMoverCoordinates(args.Actor));
        if (TryComp<StackComponent>(crystals, out var stack))
            _stack.SetCount((crystals, stack), hub.TcToRedeem);
        _popup.PopupEntity(Loc.GetString(_hands.TryPickupAnyHand(args.Actor, crystals)
            ? "contractor-payment-hands"
            : "contractor-payment-floor"), args.Actor, args.Actor);

        hub.TcPayedOut += hub.TcToRedeem;
        hub.TcToRedeem = 0;
        UpdateUi(ent, args.Actor);
    }

    /// <summary>ui_data().</summary>
    private void UpdateUi(EntityUid uplink, EntityUid user)
    {
        var state = UplinkState(uplink);
        var hub = Hub(state.LoggedInMind);
        var contracts = new List<ContractorContractData>();
        var direction = string.Empty;
        var enroute = false;

        if (hub != null)
        {
            if (hub.CurrentContract is { } current)
            {
                enroute = current.Status == ContractorContractStatus.Extracting;
                direction = DropoffDirection(user, current);
            }

            foreach (var contract in hub.Contracts)
            {
                contracts.Add(new ContractorContractData(contract.Id,
                    contract.TargetName, contract.TargetRank, contract.Payout, contract.PayoutBonus,
                    contract.DropoffName, contract.Status, contract.WantedMessage));
            }
        }

        _ui.SetUiState(uplink, ContractorUplinkUiKey.Key, new ContractorUplinkBoundUserInterfaceState(
            state.FirstLoad,
            hub != null,
            state.InfoScreen,
            state.Error,
            hub?.CurrentContract != null,
            enroute,
            direction,
            hub?.TcToRedeem ?? 0,
            hub?.TcPayedOut ?? 0,
            hub?.ContractsCompleted ?? 0,
            contracts.ToArray()));
    }

    /// <summary>dropoff_direction: стрелка к зоне или «LOCATION CONFIRMED».</summary>
    private string DropoffDirection(EntityUid user, SyndicateContract contract)
    {
        if (contract.Dropoff is not { } beacon || TerminatingOrDeleted(beacon))
            return "???";

        var userPos = _transform.GetMapCoordinates(user);
        var beaconPos = _transform.GetMapCoordinates(beacon);
        if (userPos.MapId != beaconPos.MapId)
            return "???";
        if (InArea(user, beacon))
            return Loc.GetString("contractor-location-confirmed");

        var dir = (beaconPos.Position - userPos.Position).ToWorldAngle().GetDir();
        return Loc.GetString($"contractor-direction-{dir.ToString().ToLowerInvariant()}");
    }

    private void RefreshUplinks(EntityUid mindId)
    {
        var query = EntityQueryEnumerator<ContractorUplinkComponent, ContractorUplinkStateComponent>();
        while (query.MoveNext(out var uid, out _, out var state))
        {
            if (state.LoggedInMind != mindId)
                continue;
            foreach (var actor in _ui.GetActors(uid, ContractorUplinkUiKey.Key))
            {
                UpdateUi(uid, actor);
            }
        }
    }

    #endregion

    #region Эвакуация

    /// <summary>syndicate_contract/handle_extraction(): оба в зоне — капсула садится рядом.</summary>
    private bool HandleExtraction(EntityUid mindId, SyndicateContract contract, EntityUid user)
    {
        if (TargetBody(contract) is not { } target || !InArea(user, contract.Dropoff) || !InArea(target, contract.Dropoff))
            return false;

        if (FindFreeTile(user, 3, contract.Dropoff) is not { } tile)
            return false;

        LaunchExtractionPod(mindId, contract, tile);
        return true;
    }

    /// <summary>find_obstruction_free_location(range, user, area).</summary>
    private EntityCoordinates? FindFreeTile(EntityUid center, int range, EntityUid? area)
    {
        var xform = Transform(center);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var tiles = new List<Vector2i>();
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                if (x != 0 || y != 0)
                    tiles.Add(origin + new Vector2i(x, y));
            }
        }

        _random.Shuffle(tiles);
        foreach (var tile in tiles)
        {
            if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef)
                || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                continue;

            var coords = _map.GridTileToLocal(gridUid, grid, tile);
            if (area != null)
            {
                var probe = Spawn(null, coords);
                var inside = InArea(probe, area);
                Del(probe);
                if (!inside)
                    continue;
            }

            return coords;
        }

        return null;
    }

    /// <summary>launch_extraction_pod(): метка посадки, через пару секунд — капсула.</summary>
    private void LaunchExtractionPod(EntityUid mindId, SyndicateContract contract, EntityCoordinates tile)
    {
        var landing = Spawn(PodLanding, tile);
        Schedule(PodArrival, () =>
        {
            if (!TerminatingOrDeleted(landing))
                QueueDel(landing);

            var pod = Spawn(ExtractionPod, tile);
            var podComp = EnsureComp<ContractorExtractionPodComponent>(pod);
            podComp.ContractorMind = mindId;
            podComp.ContractId = contract.Id;
            _entityStorage.OpenStorage(pod);
            _audio.PlayPvs(new SoundCollectionSpecifier("Explosion"), tile, AudioParams.Default.WithVolume(-8f));
        });
    }

    /// <summary>syndicate_contract/enter_check(): кто попал в капсулу — улетает.</summary>
    private void OnPodEntered(Entity<ContractorExtractionPodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (ent.Comp.Used || !HasComp<MobStateComponent>(args.Entity))
            return;

        ent.Comp.Used = true;
        var victim = args.Entity;
        var mindId = ent.Comp.ContractorMind;
        var hub = Hub(mindId);
        var contract = hub?.Contracts.FirstOrDefault(c => c.Id == ent.Comp.ContractId);
        var dropoff = contract?.Dropoff;

        if (hub != null && contract != null)
        {
            if (TargetBody(contract) == victim)
            {
                hub.TcToRedeem += contract.Payout;
                hub.ContractsCompleted++;
                if (!_mobState.IsDead(victim))
                    hub.TcToRedeem += contract.PayoutBonus;
                contract.Status = ContractorContractStatus.Complete;
            }
            else
            {
                // Отправить не ту цель — то же, что отменить контракт.
                contract.Status = ContractorContractStatus.Aborted;
            }

            if (hub.CurrentContract == contract)
                hub.CurrentContract = null;
        }

        if (TryComp<MindContainerComponent>(victim, out var victimMind) && victimMind.Mind is { } victimMindId)
            _kidnapped.Add(victimMindId);

        var returnCoords = _transform.GetMoverCoordinates(ent);
        var belongings = StripBelongings(victim);

        // start_exit_sequence(): капсула закрывается и улетает.
        Schedule(TimeSpan.FromSeconds(1), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            _audio.PlayPvs(PodWoosh, _transform.GetMoverCoordinates(ent));
            if (!TerminatingOrDeleted(victim))
            {
                _container.TryRemoveFromContainer(victim, force: true);
                SendToHolding(victim);
                var comp = EnsureComp<ContractorVictimComponent>(victim);
                comp.Level = 0;
                comp.NextExperience = _timing.CurTime + TimeSpan.FromSeconds(3);
                comp.ReturnAt = _timing.CurTime + TimeSpan.FromSeconds(3) + CaptureTime;
                comp.Belongings = belongings;
                comp.Dropoff = dropoff;
                comp.FallbackReturn = returnCoords;
            }

            // Остальное содержимое высыпается, улетает только жертва.
            _entityStorage.EmptyContents(ent);
            QueueDel(ent);
        });

        // finish_enter(): доля выкупа — через 3 секунды.
        if (contract is { Status: ContractorContractStatus.Complete })
        {
            var ransom = contract.Ransom * _random.Next(11, 14) / 10;
            Schedule(TimeSpan.FromSeconds(3), () => PayRansom(mindId, ransom));
        }

        RefreshUplinks(mindId);
    }

    /// <summary>Контрактник получает 35% выкупа (в SS14 — наличными).</summary>
    private void PayRansom(EntityUid mindId, int ransom)
    {
        if (CompOrNull<MindComponent>(mindId)?.OwnedEntity is not { } contractor || TerminatingOrDeleted(contractor))
            return;

        var share = (int) (ransom * 0.35f);
        var cash = Spawn(Cash, _transform.GetMoverCoordinates(contractor));
        if (TryComp<StackComponent>(cash, out var stack))
            _stack.SetCount((cash, stack), share);
        _hands.TryPickupAnyHand(contractor, cash);
        SendChat(contractor, Loc.GetString("contractor-ransom-paid", ("amount", share)));
    }

    /// <summary>gather_belongings(): забрать всё, кроме комбинезона и обуви.</summary>
    private EntityUid StripBelongings(EntityUid victim)
    {
        var holder = Spawn(null, MapCoordinates.Nullspace);
        var container = _container.EnsureContainer<Container>(holder, BelongingsContainer);

        if (TryComp<InventoryComponent>(victim, out var inventory) && _inventory.TryGetSlots(victim, out var slots))
        {
            foreach (var slot in slots)
            {
                if (slot.Name is "jumpsuit" or "shoes")
                    continue;
                if (!_inventory.TryGetSlotEntity(victim, slot.Name, out var item, inventory))
                    continue;
                if (_inventory.TryUnequip(victim, slot.Name, true, true, false, inventory))
                    _container.Insert(item.Value, container);
            }
        }

        if (TryComp<HandsComponent>(victim, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((victim, hands)).ToArray())
            {
                if (_hands.TryDrop((victim, hands), held, checkActionBlocker: false, doDropInteraction: false))
                    _container.Insert(held, container);
            }
        }

        return holder;
    }

    /// <summary>Удерживающий объект Синдиката (holdingfacility).</summary>
    private void SendToHolding(EntityUid victim)
    {
        if (!TryLoadHolding(out var grid) || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var tiles = _map.GetAllTiles(grid, gridComp)
            .Where(t => !_turf.IsSpace(t) && !_turf.IsTileBlocked(t, CollisionGroup.Impassable))
            .ToList();
        if (tiles.Count == 0)
            return;

        var tile = _random.Pick(tiles);
        _transform.SetCoordinates(victim, _map.GridTileToLocal(grid, gridComp, tile.GridIndices));
        _transform.AttachToGridOrMap(victim);
    }

    private bool TryLoadHolding(out EntityUid gridUid)
    {
        if (_holdingGrid is { } existing && Exists(existing))
        {
            gridUid = existing;
            return true;
        }

        var options = DeserializationOptions.Default with { InitializeMaps = true, PauseMaps = false };
        if (!_mapLoader.TryLoadGrid(new ResPath(HoldingMapPath), out _, out var grid, options))
        {
            gridUid = EntityUid.Invalid;
            return false;
        }

        _holdingGrid = grid.Value.Owner;
        gridUid = grid.Value.Owner;
        return true;
    }

    #endregion

    #region Жертва

    /// <summary>handle_victim_experience(): уровни воздействия на похищенного.</summary>
    private void UpdateVictims(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ContractorVictimComponent>();
        while (query.MoveNext(out var uid, out var victim))
        {
            if (now >= victim.ReturnAt)
            {
                ReturnVictim((uid, victim));
                continue;
            }

            if (victim.Level > 4 || now < victim.NextExperience || _mobState.IsDead(uid))
                continue;

            TimeSpan? next = null;
            switch (victim.Level)
            {
                case 0:
                    // Лечим, чтобы вывести из крита.
                    var omnizine = new Solution();
                    omnizine.AddReagent(Omnizine, FixedPoint2.New(20));
                    _bloodstream.TryAddToBloodstream(uid, omnizine);
                    Flash(uid);
                    _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(6));
                    SendChat(uid, Loc.GetString("contractor-victim-1"));
                    next = TimeSpan.FromSeconds(6);
                    break;
                case 1:
                    SendChat(uid, Loc.GetString("contractor-victim-2"));
                    _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(3.5));
                    next = TimeSpan.FromSeconds(6.5);
                    break;
                case 2:
                    SendChat(uid, Loc.GetString("contractor-victim-3"));
                    Flash(uid);
                    _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(5));
                    next = TimeSpan.FromSeconds(3);
                    break;
                case 3:
                    SendChat(uid, Loc.GetString("contractor-victim-4"));
                    next = TimeSpan.FromSeconds(10);
                    break;
                case 4:
                    Flash(uid);
                    _status.TryAddStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(20));
                    SendChat(uid, Loc.GetString("contractor-victim-5"));
                    _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(12));
                    break;
            }

            victim.Level++;
            if (next is { } delay)
                victim.NextExperience = now + delay;
        }
    }

    /// <summary>return_victim() + returnal_side_effects().</summary>
    private void ReturnVictim(Entity<ContractorVictimComponent> ent)
    {
        EntityCoordinates? destination = null;
        if (ent.Comp.Dropoff is { } dropoff && !TerminatingOrDeleted(dropoff))
            destination = FindFreeTile(dropoff, 6, dropoff);
        destination ??= ent.Comp.FallbackReturn;

        if (destination is { } coords)
        {
            _audio.PlayPvs(PodWoosh, coords);
            _transform.SetCoordinates(ent, coords);
            _transform.AttachToGridOrMap(ent);

            if (ent.Comp.Belongings is { } holder && !TerminatingOrDeleted(holder)
                && _container.TryGetContainer(holder, BelongingsContainer, out var container))
            {
                foreach (var item in container.ContainedEntities.ToArray())
                {
                    _container.Remove(item, container, destination: coords);
                }
            }
        }

        if (ent.Comp.Belongings is { } belongings)
            QueueDel(belongings);

        Flash(ent);
        _drunk.TryApplyDrunkenness(ent, TimeSpan.FromSeconds(5));
        SendChat(ent, Loc.GetString("contractor-victim-returned"));
        RemCompDeferred<ContractorVictimComponent>(ent);
    }

    private void Flash(EntityUid uid)
    {
        _flash.Flash(uid, null, null, TimeSpan.FromSeconds(3), 0.8f, displayPopup: false);
    }

    private void SendChat(EntityUid uid, string text)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            _chat.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, actor.PlayerSession.Channel);
    }

    #endregion

    #region Аплинк Синдиката

    /// <summary>uplink_item/contractor/reroll: новые цели для неактивных контрактов.</summary>
    private void OnReroll(Entity<MindContainerComponent> ent, ref ContractorRerollEvent args)
    {
        if (ent.Comp.Mind is not { } mindId || Hub(mindId) is not { } hub)
            return;

        var targets = new HashSet<EntityUid>();
        foreach (var contract in hub.Contracts)
        {
            if (contract.Status is ContractorContractStatus.Active or ContractorContractStatus.Inactive)
                continue;
            if (contract.TargetMind is { } target)
                targets.Add(target);
        }

        foreach (var contract in hub.Contracts)
        {
            if (contract.Status is not (ContractorContractStatus.Active or ContractorContractStatus.Inactive))
                continue;
            Generate(mindId, contract, targets);
            if (contract.TargetMind is { } target)
                targets.Add(target);
        }

        hub.AssignedTargets = targets;
        RefreshUplinks(mindId);
    }

    #endregion

    #region Пинпоинтер

    /// <summary>Пинпоинтер навсегда привязывается к первому взявшему его контрактнику.</summary>
    private void OnPinpointerPickedUp(Entity<ContractorPinpointerComponent> ent, ref GotEquippedHandEvent args)
    {
        if (ent.Comp.ContractorMind != null || !_mind.TryGetMind(args.User, out var mindId, out _))
            return;
        ent.Comp.ContractorMind = mindId;
    }

    private void UpdatePinpointers(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ContractorPinpointerComponent, PinpointerComponent>();
        while (query.MoveNext(out var uid, out var comp, out var pinpointer))
        {
            if (Hub(comp.ContractorMind)?.CurrentContract is not { } contract || TargetBody(contract) is not { } target)
            {
                ClearPinpointer((uid, comp), pinpointer);
                continue;
            }

            if (comp.TargetEntity == target && comp.DecoyEntity is { } decoy && Exists(decoy) && now < comp.NextRefreshAt)
                continue;

            // minimum_range = 25: точность низкая, указывает в окрестность цели.
            var xform = Transform(target);
            if (xform.MapID == MapId.Nullspace)
            {
                ClearPinpointer((uid, comp), pinpointer);
                continue;
            }

            var coords = xform.Coordinates.Offset(_random.NextAngle().ToVec() * _random.NextFloat(2.5f, 4.5f));
            if (comp.DecoyEntity is not { } marker || !Exists(marker))
            {
                marker = Spawn(PinpointerMarker, coords);
                comp.DecoyEntity = marker;
            }
            else
            {
                _transform.SetCoordinates(marker, coords);
            }

            comp.TargetEntity = target;
            comp.NextRefreshAt = now + PinpointerRefreshDelay;
            _pinpointer.SetTarget((uid, pinpointer), marker);
        }
    }

    private void ClearPinpointer(Entity<ContractorPinpointerComponent> ent, PinpointerComponent pinpointer)
    {
        if (ent.Comp.DecoyEntity is { } decoy && Exists(decoy))
            QueueDel(decoy);
        if (ent.Comp.TargetEntity == null && ent.Comp.DecoyEntity == null)
            return;
        ent.Comp.DecoyEntity = null;
        ent.Comp.TargetEntity = null;
        _pinpointer.SetTarget((ent.Owner, pinpointer), null);
    }

    #endregion

    /// <summary>Итоги раунда предателя: выполненные контракты и заработанные ТК.</summary>
    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        var lines = new List<string>();
        var query = EntityQueryEnumerator<ContractorHubComponent, MindComponent>();
        while (query.MoveNext(out _, out var hub, out var mind))
        {
            lines.Add(Loc.GetString("contractor-round-end-line",
                ("name", mind.CharacterName ?? "?"),
                ("completed", hub.ContractsCompleted),
                ("tc", hub.TcPayedOut + hub.TcToRedeem)));
        }

        if (lines.Count == 0)
            return;

        ev.AddLine(Loc.GetString("contractor-round-end-header"));
        foreach (var line in lines)
        {
            ev.AddLine(line);
        }

        ev.AddLine(string.Empty);
    }

    /// <summary>Админ-команда: выдать предателю набор контрактника.</summary>
    public bool TryGiveKit(ICommonSession player, out string error)
    {
        error = string.Empty;
        if (player.AttachedEntity is not { } body)
        {
            error = Loc.GetString("contractor-command-no-body");
            return false;
        }

        if (!_mind.TryGetMind(body, out var mindId, out _) || !_roles.MindHasRole<TraitorRoleComponent>(mindId))
        {
            error = Loc.GetString("contractor-command-not-traitor");
            return false;
        }

        var kit = Spawn("BoxContractorKit", _transform.GetMoverCoordinates(body));
        _hands.TryPickupAnyHand(body, kit);
        return true;
    }

    private void Schedule(TimeSpan delay, Action act)
    {
        _scheduled.Add((_timing.CurTime + delay, act));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        for (var i = _scheduled.Count - 1; i >= 0; i--)
        {
            if (now < _scheduled[i].At)
                continue;
            var act = _scheduled[i].Act;
            _scheduled.RemoveAt(i);
            act();
        }

        UpdateVictims(now);
        UpdatePinpointers(now);
    }
}
