using System.Linq;
using System.Numerics;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Light.Components;
using Content.Server.MassMedia.Systems;
using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Server.Storage.EntitySystems;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Contractor;
using Content.Shared.Imperial.Contractor.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Store.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Contractor;

public sealed class ContractorSystem : EntitySystem
{
    private static readonly Color ContractorBriefingColor = Color.FromHex("#7fbbe6");
    private const string ContractorCurrency = "ContractorRep";
    private const string ContractorCandidateRole = "MindRoleContractorCandidate";
    private const string ContractorRole = "MindRoleContractor";
    private const string PirateMapPath = "/Maps/Shuttles/contractor_prison.yml";
    private const string PrisonLocker = "LockerPrisoner";
    private const string PrisonUniform = "ClothingUniformJumpsuitPrisoner";
    private const string PrisonShoes = "ClothingShoesColorOrange";
    private const string FalsefireFlarePrototype = "ContractorFalsefireFlare";
    private const string FalsefirePortalPrototype = "ContractorFalsefirePortal";
    private const string ContractorPinpointerMarkerPrototype = "ContractorPinpointerMarker";

    private const float CandidateChance = 1.0f;
    private const float DeliveryRange = 2f;
    private const int OfferCapacity = 6;
    private const float PinpointerMinOffset = 2.5f;
    private const float PinpointerMaxOffset = 4.5f;
    private const float PortalOffset = 0.8f;
    private const float StoreLinkRange = 2f;
    private const float UpdateInterval = 0.5f;
    private const float DeadPayoutRatio = 0.2f;
    private static readonly TimeSpan FalsefireIgnitionDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PinpointerRefreshDelay = TimeSpan.FromSeconds(2.5);

    private static readonly TimeSpan DetentionDuration = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan PortalLifetime = TimeSpan.FromSeconds(45);

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NewsSystem _news = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PinpointerSystem _pinpointer = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, ContractorProfile> _profiles = new();
    private readonly Dictionary<EntityUid, DetainedTarget> _detainedTargets = new();
    private readonly HashSet<EntityUid> _completedExtractedMinds = new();
    private float _accumulator;
    private EntityUid? _pirateGridUid;
    private bool _candidatesSelected;

    private static readonly RequisitionDefinition[] Requisitions =
    {
        new("reroll", null, 2),
        new("pinpointer", "ContractorPinpointer", 1),
        new("extraction", "BoxContractorExtraction", 1),
        new("zippo", "ContractorFlippoLighter", 12),
        new("balloon", "ContractorBalloon", 12),
    };

    private static readonly string[] EasyKeywords =
    {
        "library", "botan", "hydro", "anomaly generator", "dam", "security checkpoint", "checkpoint", "cryonics",
        "xenoarch", "xenoarchaeology", "kitchen", "workshop", "morgue", "operating", "arrivals shuttle", "departure",
        "janitor closet", "janitor", "cargo", "arrivals", "escape pod", "court", "theater", "chapel", "evacuation",
        "библиот", "ботан", "генератор аномал", "дам", "кпп сб", "крионик", "ксеноарх", "кухн", "мастерск",
        "морг", "операцион", "отбыт", "подсобка убор", "поставк", "прибыт", "спасательн", "суд", "театр",
        "церк", "эвакуац",
    };

    private static readonly string[] MediumKeywords =
    {
        "gateway", "atmos", "gravity generator", "engineering", "cryo sleep", "detective", "medical",
        "medbay", "science", "robotics", "supply", "cargo reception", "rnd", "r&d", "teg", "disposals",
        "eva storage", "power storage", "gatewat", "атмос", "генератор гравитац", "инженерн", "капсулы криосна",
        "комната детектив", "медецис", "медицин", "медотдел", "научн", "робототех", "снабж", "приемная снабж",
        "рнд", "тэг", "утилиз", "хранилище ева", "хранилище энергии",
    };

    private static readonly string[] HardKeywords =
    {
        "bar", "security", "brig", "dorm", "ai sat", "ai core", "ai", "chief medical officer", "captain's quarters",
        "captain", "command", "chief engineer", "research director", "head of personnel", "bridge", "armory",
        "head of personnel office", "warden", "lawyer", "perma", "server", "singularity", "solar", "telecom",
        "chemistry", "vault", "хранилище плат", "ядро ии", "бар", "безопас", "бриг", "дорм", "ии спутник",
        "ии", "кабинет гв", "каюта капит", "комадн", "командн", "комната кма", "комната нра", "комната си",
        "мостик", "оружейн", "офис гп", "офис гсб", "офис смотрит", "офис юрист", "перма", "серверн",
        "сингуляр", "солнечн", "телеком", "хими", "хранилищ",
    };

    private static readonly string[] EasyReasonTemplates =
    {
        "contractor-reason-easy-1",
        "contractor-reason-easy-2",
        "contractor-reason-easy-3",
    };

    private static readonly string[] MediumReasonTemplates =
    {
        "contractor-reason-medium-1",
        "contractor-reason-medium-2",
        "contractor-reason-medium-3",
    };

    private static readonly string[] HardReasonTemplates =
    {
        "contractor-reason-hard-1",
        "contractor-reason-hard-2",
        "contractor-reason-hard-3",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnTraitorSelected);
        SubscribeLocalEvent<MindContainerComponent, ContractorKitPurchasedEvent>(OnContractorKitPurchased);
        SubscribeLocalEvent<ContractorUplinkComponent, ActivatableUIOpenAttemptEvent>(OnUplinkOpenAttempt);
        SubscribeLocalEvent<ContractorUplinkComponent, BoundUIOpenedEvent>(OnUplinkOpened);

        Subs.BuiEvents<ContractorUplinkComponent>(ContractorUplinkUiKey.Key, subs =>
        {
            subs.Event<ContractorAcceptContractMessage>(OnAcceptContract);
            subs.Event<ContractorDeclineContractMessage>(OnDeclineContract);
            subs.Event<ContractorBuyRequisitionMessage>(OnBuyRequisition);
            subs.Event<ContractorOpenPortalMessage>(OnOpenPortal);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator -= UpdateInterval;

        UpdateActiveContracts();
        UpdateContractorPinpointers();
        UpdateFalsefires();
        UpdatePortals();
        UpdateDetentions();
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        if (_candidatesSelected)
            return;

        _candidatesSelected = true;
        SelectContractorCandidates();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _profiles.Clear();
        _detainedTargets.Clear();
        _completedExtractedMinds.Clear();
        _pirateGridUid = null;
        _accumulator = 0f;
        _candidatesSelected = false;
    }

    private void OnTraitorSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (!HasComp<TraitorRuleComponent>(args.GameRule.Owner))
            return;

        if (_players.PlayerCount != 1)
            return;

        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind) || mind.OwnedEntity is not { } owner)
            return;

        if (!_roles.MindHasRole<TraitorRoleComponent>(mindId))
            return;

        if (_roles.MindHasRole<ContractorRoleComponent>(mindId) || _roles.MindHasRole<ContractorCandidateRoleComponent>(mindId))
            return;

        var traitorCount = 0;
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var otherMindId, out _))
        {
            if (_roles.MindHasRole<TraitorRoleComponent>(otherMindId))
                traitorCount++;
        }

        if (traitorCount != 1)
            return;

        _roles.MindAddRole(mindId, ContractorCandidateRole, mind);
        _antag.SendBriefing(owner, Loc.GetString("contractor-offer-available"), ContractorBriefingColor, null);
        RefreshTraitorUplink(owner);
    }

    private void SelectContractorCandidates()
    {
        var activeTraitorRules = new List<List<EntityUid>>();
        var query = EntityQueryEnumerator<TraitorRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var rule, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            activeTraitorRules.Add(rule.TraitorMinds.Distinct().ToList());
        }

        var forceSingleTraitorCandidate = activeTraitorRules.Any(traitors => traitors.Count == 1);
        if (!forceSingleTraitorCandidate && !_random.Prob(CandidateChance))
            return;

        foreach (var traitors in activeTraitorRules)
        {
            var count = traitors.Count == 1 ? 1 : traitors.Count / 4;
            if (count <= 0)
                continue;

            var pool = traitors
                .Where(mindId => !_roles.MindHasRole<ContractorRoleComponent>(mindId))
                .Where(mindId => !_roles.MindHasRole<ContractorCandidateRoleComponent>(mindId))
                .ToList();

            while (pool.Count > 0 && count > 0)
            {
                var index = _random.Next(pool.Count);
                var candidateMindId = pool[index];
                pool.RemoveAt(index);
                count--;

                if (!TryComp<MindComponent>(candidateMindId, out var mind) || mind.OwnedEntity is not { } owner)
                    continue;

                _roles.MindAddRole(candidateMindId, ContractorCandidateRole, mind);
                _antag.SendBriefing(owner, Loc.GetString("contractor-offer-available"), ContractorBriefingColor, null);
                RefreshTraitorUplink(owner);
            }
        }
    }

    private void OnContractorKitPurchased(Entity<MindContainerComponent> ent, ref ContractorKitPurchasedEvent args)
    {
        if (!_mind.TryGetMind(ent.Owner, out var mindId, out var mind))
            return;

        if (!_roles.MindHasRole<TraitorRoleComponent>(mindId))
            return;

        if (!_roles.MindHasRole<ContractorCandidateRoleComponent>(mindId))
            return;

        if (_roles.MindHasRole<ContractorRoleComponent>(mindId))
            return;

        _roles.MindAddRole(mindId, ContractorRole, mind);
        TryAssignNearbyContractorStore(ent.Owner, mindId);
        EnsureProfile(mindId, ent.Owner);

        _antag.SendBriefing(ent.Owner, Loc.GetString("contractor-role-greeting"), ContractorBriefingColor, null);
    }

    public bool TryMakeContractor(ICommonSession player, out string error)
    {
        error = string.Empty;

        if (player.AttachedEntity is not { } owner)
        {
            error = "Player has no attached entity.";
            return false;
        }

        if (!_mind.TryGetMind(owner, out var mindId, out var mind))
        {
            error = "Player has no mind.";
            return false;
        }

        if (!_roles.MindHasRole<TraitorRoleComponent>(mindId))
        {
            error = "Player must already be a traitor.";
            return false;
        }

        if (_roles.MindHasRole<ContractorRoleComponent>(mindId))
        {
            error = "Player is already a contractor.";
            return false;
        }

        if (!_roles.MindHasRole<ContractorCandidateRoleComponent>(mindId))
        {
            _roles.MindAddRole(mindId, ContractorCandidateRole, mind);
            RefreshTraitorUplink(owner);
        }

        _roles.MindAddRole(mindId, ContractorRole, mind);
        TryAssignNearbyContractorStore(owner, mindId);
        EnsureProfile(mindId, owner);

        _antag.SendBriefing(owner, Loc.GetString("contractor-role-greeting"), ContractorBriefingColor, null);
        return true;
    }

    private void OnUplinkOpenAttempt(Entity<ContractorUplinkComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!_mind.TryGetMind(args.User, out var mindId, out _)
            || !_roles.MindHasRole<ContractorRoleComponent>(mindId))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("contractor-uplink-denied"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        TryBindStoreToMind(ent.Owner, mindId);
    }

    private void OnUplinkOpened(Entity<ContractorUplinkComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        UpdateUplinkUi(ent.Owner, mindId);
    }

    private void OnAcceptContract(Entity<ContractorUplinkComponent> ent, ref ContractorAcceptContractMessage args)
    {
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        var profile = EnsureProfile(mindId, args.Actor);
        if (profile.ActiveOffer != null)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        var contractId = args.ContractId;
        var offer = profile.Offers.FirstOrDefault(x => x.Id == contractId);
        if (offer == null)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        var difficulty = args.Difficulty;
        var option = offer.Options.FirstOrDefault(x => x.Difficulty == difficulty);
        if (option == null)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        profile.Offers.Remove(offer);
        profile.ActiveOffer = new ActiveContract(
            offer.Id,
            offer.TargetEntity,
            offer.TargetName,
            offer.Job,
            option.BeaconUid,
            option.BeaconLabel,
            option.Difficulty,
            option.Reason,
            option.AlivePayout,
            option.DeadPayout);
        profile.Stage = ContractorContractStage.AwaitingBeacon;
        profile.BeaconAnnounced = false;
        profile.FalsefireFlare = null;
        profile.FalsefireIgnitionAt = null;

        _antag.SendBriefing(args.Actor,
            Loc.GetString("contractor-contract-accepted",
                ("target", offer.TargetName),
                ("job", offer.Job),
                ("location", option.BeaconLabel),
                ("difficulty", Loc.GetString($"contractor-difficulty-{difficulty.ToString().ToLowerInvariant()}"))),
            ContractorBriefingColor,
            null);

        UpdateUplinkUi(ent.Owner, mindId);
    }

    private void OnDeclineContract(Entity<ContractorUplinkComponent> ent, ref ContractorDeclineContractMessage args)
    {
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        var profile = EnsureProfile(mindId, args.Actor);
        var contractId = args.ContractId;
        var offer = profile.Offers.FirstOrDefault(x => x.Id == contractId);
        if (offer == null)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        profile.Offers.Remove(offer);
        profile.BlockedTargets.Add(offer.TargetEntity);
        EnsureOfferCapacity(mindId, args.Actor, profile);

        _antag.SendBriefing(args.Actor,
            Loc.GetString("contractor-contract-declined", ("target", offer.TargetName)),
            ContractorBriefingColor,
            null);

        UpdateUplinkUi(ent.Owner, mindId);
    }

    private void OnBuyRequisition(Entity<ContractorUplinkComponent> ent, ref ContractorBuyRequisitionMessage args)
    {
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        if (!TryComp<StoreComponent>(ent, out var store))
            return;

        TryBindStoreToMind(ent.Owner, mindId, store);

        var requisitionId = args.RequisitionId;
        var requisition = Requisitions.FirstOrDefault(x => x.Id == requisitionId);
        if (requisition == null)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        if (!store.Balance.TryGetValue(ContractorCurrency, out var rep) || rep < requisition.Cost)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        store.Balance[ContractorCurrency] -= requisition.Cost;
        Dirty(ent.Owner, store);

        if (requisition.Id == "reroll")
        {
            var profile = EnsureProfile(mindId, args.Actor);
            profile.Offers.Clear();
            EnsureOfferCapacity(mindId, args.Actor, profile);
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        if (requisition.Prototype != null)
        {
            var spawned = Spawn(requisition.Prototype, Transform(args.Actor).Coordinates);
            BindContractorPinpointer(spawned, mindId);
            _hands.TryPickupAnyHand(args.Actor, spawned, checkActionBlocker: false);
        }

        UpdateUplinkUi(ent.Owner, mindId);
    }

    private void OnOpenPortal(Entity<ContractorUplinkComponent> ent, ref ContractorOpenPortalMessage args)
    {
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        var profile = EnsureProfile(mindId, args.Actor);
        if (profile.ActiveOffer == null || profile.Stage != ContractorContractStage.PortalReady)
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        if ((profile.Portal != null && Exists(profile.Portal.Value)) || (profile.FalsefireFlare != null && Exists(profile.FalsefireFlare.Value)))
        {
            UpdateUplinkUi(ent.Owner, mindId);
            return;
        }

        var flare = Spawn(FalsefireFlarePrototype, Transform(args.Actor).Coordinates.Offset(new Vector2(PortalOffset, 0f)));
        _hands.TryPickupAnyHand(args.Actor, flare, checkActionBlocker: false);
        profile.FalsefireFlare = flare;
        profile.FalsefireIgnitionAt = null;

        _antag.SendBriefing(args.Actor, Loc.GetString("contractor-falsefire-issued"), ContractorBriefingColor, null);
        UpdateUplinkUi(ent.Owner, mindId);
    }

    private void UpdateActiveContracts()
    {
        foreach (var (mindId, profile) in _profiles)
        {
            if (profile.ActiveOffer == null || profile.Stage != ContractorContractStage.AwaitingBeacon)
                continue;

            if (!Exists(profile.ActiveOffer.TargetEntity) || !Exists(profile.ActiveOffer.BeaconUid))
                continue;

            var target = profile.ActiveOffer.TargetEntity;
            var targetXform = Transform(target);
            var beaconXform = Transform(profile.ActiveOffer.BeaconUid);
            if (targetXform.MapID != beaconXform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(targetXform) - _transform.GetWorldPosition(beaconXform)).Length();
            if (distance > DeliveryRange)
                continue;

            profile.Stage = ContractorContractStage.PortalReady;

            if (!profile.BeaconAnnounced && TryGetMindOwner(mindId, out var owner))
            {
                profile.BeaconAnnounced = true;
                _antag.SendBriefing(owner,
                    Loc.GetString("contractor-beacon-reached", ("location", profile.ActiveOffer.BeaconLabel)),
                    ContractorBriefingColor,
                    null);
            }

            if (TryFindContractorStoreFromMind(mindId, out var uplinkUid, out _))
                UpdateUplinkUi(uplinkUid, mindId);
        }
    }

    private void UpdatePortals()
    {
        var query = EntityQueryEnumerator<ContractorPortalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var portal, out var xform))
        {
            if (_timing.CurTime >= portal.ExpireAt)
            {
                CleanupPortal(uid, portal.ContractorMind);
                continue;
            }

            if (!_profiles.TryGetValue(portal.ContractorMind, out var profile) || profile.ActiveOffer == null)
            {
                CleanupPortal(uid, portal.ContractorMind);
                continue;
            }

            if (!Exists(portal.TargetEntity))
                continue;

            var target = portal.TargetEntity;
            if (Transform(target).MapID != xform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(xform)).Length();
            if (distance > portal.ActivationRange)
                continue;

            CompleteTransfer(portal.ContractorMind, target, uid);
        }
    }

    private void UpdateFalsefires()
    {
        foreach (var (mindId, profile) in _profiles)
        {
            if (profile.ActiveOffer == null || profile.FalsefireFlare == null)
                continue;

            var flareUid = profile.FalsefireFlare.Value;
            if (!Exists(flareUid))
            {
                profile.FalsefireFlare = null;
                profile.FalsefireIgnitionAt = null;

                if (TryFindContractorStoreFromMind(mindId, out var uplinkUid, out _))
                    UpdateUplinkUi(uplinkUid, mindId);

                continue;
            }

            if (!TryComp<ExpendableLightComponent>(flareUid, out var flareLight) || !flareLight.Activated)
                continue;

            if (profile.FalsefireIgnitionAt == null)
            {
                profile.FalsefireIgnitionAt = _timing.CurTime + FalsefireIgnitionDelay;

                if (TryGetMindOwner(mindId, out var owner))
                    _antag.SendBriefing(owner, Loc.GetString("contractor-falsefire-ignited"), ContractorBriefingColor, null);

                if (TryFindContractorStoreFromMind(mindId, out var uplinkUid, out _))
                    UpdateUplinkUi(uplinkUid, mindId);

                continue;
            }

            if (_timing.CurTime < profile.FalsefireIgnitionAt.Value)
                continue;

            var portal = Spawn(FalsefirePortalPrototype, Transform(flareUid).Coordinates);
            var portalComp = EnsureComp<ContractorPortalComponent>(portal);
            portalComp.ContractorMind = mindId;
            portalComp.TargetEntity = profile.ActiveOffer.TargetEntity;
            portalComp.ExpireAt = _timing.CurTime + PortalLifetime;
            Dirty(portal, portalComp);

            QueueDel(flareUid);
            profile.FalsefireFlare = null;
            profile.FalsefireIgnitionAt = null;
            profile.Portal = portal;

            if (TryGetMindOwner(mindId, out var owner2))
                _antag.SendBriefing(owner2, Loc.GetString("contractor-falsefire-opened"), ContractorBriefingColor, null);

            if (TryFindContractorStoreFromMind(mindId, out var uplinkUid2, out _))
                UpdateUplinkUi(uplinkUid2, mindId);
        }
    }

    private void UpdateContractorPinpointers()
    {
        var query = EntityQueryEnumerator<ContractorPinpointerComponent, PinpointerComponent>();
        while (query.MoveNext(out var uid, out var contractorPinpointer, out var pinpointer))
        {
            if (contractorPinpointer.ContractorMind is not { } contractorMind)
                continue;

            if (!_profiles.TryGetValue(contractorMind, out var profile) || profile.ActiveOffer == null || !Exists(profile.ActiveOffer.TargetEntity))
            {
                ClearPinpointerTarget((uid, contractorPinpointer), pinpointer);
                continue;
            }

            var target = profile.ActiveOffer.TargetEntity;
            if (contractorPinpointer.TargetEntity != target
                || contractorPinpointer.DecoyEntity == null
                || !Exists(contractorPinpointer.DecoyEntity.Value)
                || _timing.CurTime >= contractorPinpointer.NextRefreshAt)
            {
                RefreshPinpointerDecoy((uid, contractorPinpointer), pinpointer, target);
            }
        }
    }

    private void BindContractorPinpointer(EntityUid pinpointerUid, EntityUid mindId)
    {
        if (!TryComp<ContractorPinpointerComponent>(pinpointerUid, out var contractorPinpointer))
            return;

        contractorPinpointer.ContractorMind = mindId;
        contractorPinpointer.NextRefreshAt = TimeSpan.Zero;
        contractorPinpointer.TargetEntity = null;
        contractorPinpointer.DecoyEntity = null;
    }

    private void RefreshPinpointerDecoy(Entity<ContractorPinpointerComponent> ent, PinpointerComponent pinpointer, EntityUid target)
    {
        var coords = GetApproximateTargetCoordinates(target);
        if (coords == null)
        {
            ClearPinpointerTarget(ent, pinpointer);
            return;
        }

        var marker = ent.Comp.DecoyEntity;
        if (marker == null || !Exists(marker.Value))
        {
            marker = Spawn(ContractorPinpointerMarkerPrototype, coords.Value);
            ent.Comp.DecoyEntity = marker;
        }
        else
        {
            _transform.SetCoordinates(marker.Value, coords.Value);
        }

        ent.Comp.TargetEntity = target;
        ent.Comp.NextRefreshAt = _timing.CurTime + PinpointerRefreshDelay;

        _pinpointer.SetTarget((ent.Owner, pinpointer), marker.Value);
    }

    private void ClearPinpointerTarget(Entity<ContractorPinpointerComponent> ent, PinpointerComponent pinpointer)
    {
        if (ent.Comp.DecoyEntity is { } decoy && Exists(decoy))
            QueueDel(decoy);

        ent.Comp.DecoyEntity = null;
        ent.Comp.TargetEntity = null;
        ent.Comp.NextRefreshAt = TimeSpan.Zero;
        _pinpointer.SetTarget((ent.Owner, pinpointer), null);
    }

    private EntityCoordinates? GetApproximateTargetCoordinates(EntityUid target)
    {
        if (!Exists(target))
            return null;

        var xform = Transform(target);
        if (xform.MapID == MapId.Nullspace)
            return null;

        var direction = _random.NextAngle().ToVec();
        var distance = _random.NextFloat(PinpointerMinOffset, PinpointerMaxOffset);
        return xform.Coordinates.Offset(direction * distance);
    }

    private void UpdateDetentions()
    {
        var due = _detainedTargets
            .Where(x => _timing.CurTime >= x.Value.ReleaseAt)
            .Select(x => x.Key)
            .ToArray();

        foreach (var targetEntity in due)
        {
            if (!_detainedTargets.Remove(targetEntity, out var detained))
                continue;

            if (!Exists(detained.Locker))
                continue;

            _transform.SetCoordinates(detained.Locker, detained.ReturnCoordinates);

            if (Exists(targetEntity))
                _entityStorage.Insert(targetEntity, detained.Locker);
        }
    }

    private void CompleteTransfer(EntityUid contractorMind, EntityUid target, EntityUid portalUid)
    {
        if (!_profiles.TryGetValue(contractorMind, out var profile) || profile.ActiveOffer == null)
            return;

        if (!TryLoadPirateMap(out var pirateGridUid))
        {
            CleanupPortal(portalUid, contractorMind);
            return;
        }

        var offer = profile.ActiveOffer;
        var returnCoords = Transform(target).Coordinates;
        var dead = IsTargetDead(offer.TargetEntity);
        var payout = dead ? offer.DeadPayout : offer.AlivePayout;

        if (!dead && TryGetTargetMind(offer.TargetEntity, out var targetMind))
            _completedExtractedMinds.Add(targetMind);

        var locker = Spawn(PrisonLocker, new EntityCoordinates(pirateGridUid, new Vector2(1f, 0f)));
        StripTargetIntoLocker(target, locker);

        if (!dead)
            EquipPrisonOutfit(target);

        _transform.SetCoordinates(target, new EntityCoordinates(pirateGridUid, Vector2.Zero));
        _detainedTargets[offer.TargetEntity] = new DetainedTarget(locker, returnCoords, _timing.CurTime + DetentionDuration);

        if (TryGetMindOwner(contractorMind, out var contractor))
        {
            TryPayTelecrystals(contractor, payout);
            TryPayReputation(contractor, contractorMind, 2);
            _antag.SendBriefing(contractor,
                Loc.GetString("contractor-contract-complete", ("amount", payout), ("reputation", FixedPoint2.New(2))),
                ContractorBriefingColor,
                null);
        }

        PublishNews(target, offer);

        profile.ActiveOffer = null;
        profile.Stage = ContractorContractStage.None;
        profile.Portal = null;
        profile.FalsefireFlare = null;
        profile.FalsefireIgnitionAt = null;
        profile.BeaconAnnounced = false;
        profile.BlockedTargets.Add(offer.TargetEntity);

        if (TryGetMindOwner(contractorMind, out var owner))
            EnsureOfferCapacity(contractorMind, owner, profile);

        CleanupPortal(portalUid, contractorMind, true);
    }

    private void PublishNews(EntityUid target, ActiveContract offer)
    {
        _news.TryAddNews(
            target,
            Loc.GetString("contractor-news-title", ("target", offer.TargetName)),
            Loc.GetString("contractor-news-content", ("target", offer.TargetName), ("job", offer.Job), ("reason", offer.Reason)),
            out _,
            Loc.GetString("contractor-news-author"));
    }

    private ContractorProfile EnsureProfile(EntityUid mindId, EntityUid owner)
    {
        if (_profiles.TryGetValue(mindId, out var existing))
            return existing;

        var profile = new ContractorProfile();
        _profiles[mindId] = profile;
        EnsureOfferCapacity(mindId, owner, profile);
        return profile;
    }

    private void EnsureOfferCapacity(EntityUid mindId, EntityUid owner, ContractorProfile profile)
    {
        var beaconPools = GetBeaconPools(_station.GetOwningStation(owner));
        var targets = GetAvailableTargets(mindId, owner);

        while (profile.Offers.Count < OfferCapacity)
        {
            if (targets.Count == 0)
                break;

            var targetIndex = _random.Next(targets.Count);
            var targetEntity = targets[targetIndex];
            targets.RemoveAt(targetIndex);

            var offer = BuildOffer(targetEntity, beaconPools);
            if (offer == null)
                continue;

            profile.Offers.Add(offer);
        }
    }

    private List<EntityUid> GetAvailableTargets(EntityUid contractorMind, EntityUid contractorOwner)
    {
        var taken = new HashSet<EntityUid>();
        foreach (var profile in _profiles.Values)
        {
            foreach (var offer in profile.Offers)
            {
                taken.Add(offer.TargetEntity);
            }

            if (profile.ActiveOffer != null)
                taken.Add(profile.ActiveOffer.TargetEntity);
        }

        var blocked = _profiles.TryGetValue(contractorMind, out var current)
            ? current.BlockedTargets
            : new HashSet<EntityUid>();

        var output = new List<EntityUid>();
        var stationUid = _station.GetOwningStation(contractorOwner);
        var query = EntityQueryEnumerator<HumanoidProfileComponent, TransformComponent>();
        while (query.MoveNext(out var targetUid, out _, out var xform))
        {
            if (targetUid == contractorOwner || taken.Contains(targetUid) || blocked.Contains(targetUid))
                continue;

            if (xform.MapID == MapId.Nullspace || !HasComp<Content.Shared.Mobs.Components.MobStateComponent>(targetUid))
                continue;

            if (stationUid != null && _station.GetOwningStation(targetUid) != stationUid)
                continue;

            if (TryGetTargetMind(targetUid, out var targetMindId))
            {
                if (targetMindId == contractorMind)
                    continue;

                if (_roles.MindGetAllRoleInfo(targetMindId).Any(role => role.Antagonist))
                    continue;
            }

            output.Add(targetUid);
        }

        return output;
    }

    private Dictionary<ContractorDifficulty, List<(EntityUid Uid, string Label)>> GetBeaconPools(EntityUid? stationUid)
    {
        var output = new Dictionary<ContractorDifficulty, List<(EntityUid, string)>>
        {
            [ContractorDifficulty.Easy] = new(),
            [ContractorDifficulty.Medium] = new(),
            [ContractorDifficulty.Hard] = new(),
        };

        var query = EntityQueryEnumerator<NavMapBeaconComponent>();
        while (query.MoveNext(out var beaconUid, out var beacon))
        {
            var label = beacon.Text;
            if (string.IsNullOrWhiteSpace(label))
                continue;

            if (stationUid != null && _station.GetOwningStation(beaconUid) != stationUid)
                continue;

            output[ClassifyBeacon(label)].Add((beaconUid, label));
        }

        if (output[ContractorDifficulty.Medium].Count == 0)
        {
            output[ContractorDifficulty.Medium].AddRange(output[ContractorDifficulty.Easy]);
            output[ContractorDifficulty.Medium].AddRange(output[ContractorDifficulty.Hard]);
        }

        if (output[ContractorDifficulty.Easy].Count == 0)
            output[ContractorDifficulty.Easy].AddRange(output[ContractorDifficulty.Medium]);

        if (output[ContractorDifficulty.Hard].Count == 0)
            output[ContractorDifficulty.Hard].AddRange(output[ContractorDifficulty.Medium]);

        return output;
    }

    private ContractorDifficulty ClassifyBeacon(string label)
    {
        var lowered = label.ToLowerInvariant();
        if (HardKeywords.Any(lowered.Contains))
            return ContractorDifficulty.Hard;

        if (EasyKeywords.Any(lowered.Contains))
            return ContractorDifficulty.Easy;

        if (MediumKeywords.Any(lowered.Contains))
            return ContractorDifficulty.Medium;

        return ContractorDifficulty.Medium;
    }

    private ContractOffer? BuildOffer(EntityUid targetEntity, Dictionary<ContractorDifficulty, List<(EntityUid Uid, string Label)>> beaconPools)
    {
        if (!Exists(targetEntity))
            return null;

        var targetName = Name(targetEntity);
        var job = Loc.GetString("contractor-target-job-unknown");
        if (TryGetTargetMind(targetEntity, out var targetMindId))
        {
            var jobRole = _roles.MindGetAllRoleInfo(targetMindId).FirstOrDefault(role => !role.Antagonist);
            if (!string.IsNullOrEmpty(jobRole.Name))
                job = Loc.GetString(jobRole.Name);
        }

        var options = new List<ContractOption>();
        foreach (var difficulty in new[] { ContractorDifficulty.Easy, ContractorDifficulty.Medium, ContractorDifficulty.Hard })
        {
            if (beaconPools[difficulty].Count == 0)
                continue;

            var beacon = _random.Pick(beaconPools[difficulty]);
            var alive = difficulty switch
            {
                ContractorDifficulty.Easy => FixedPoint2.New(_random.Next(2, 4)),
                ContractorDifficulty.Medium => FixedPoint2.New(_random.Next(5, 7)),
                ContractorDifficulty.Hard => FixedPoint2.New(_random.Next(6, 9)),
                _ => FixedPoint2.Zero,
            };

            options.Add(new ContractOption(
                difficulty,
                beacon.Uid,
                beacon.Label,
                GenerateReason(targetName, beacon.Label, difficulty),
                alive,
                alive * DeadPayoutRatio));
        }

        if (options.Count == 0)
            return null;

        return new ContractOffer(
            $"{targetEntity}",
            targetEntity,
            targetName,
            job,
            options);
    }

    private string GenerateReason(string targetName, string beaconLabel, ContractorDifficulty difficulty)
    {
        var key = difficulty switch
        {
            ContractorDifficulty.Easy => _random.Pick(EasyReasonTemplates),
            ContractorDifficulty.Medium => _random.Pick(MediumReasonTemplates),
            ContractorDifficulty.Hard => _random.Pick(HardReasonTemplates),
            _ => EasyReasonTemplates[0],
        };

        return Loc.GetString(key, ("target", targetName), ("location", beaconLabel));
    }

    private void UpdateUplinkUi(EntityUid uplinkUid, EntityUid mindId)
    {
        if (!TryComp<StoreComponent>(uplinkUid, out var store) || !TryGetMindOwner(mindId, out var owner))
            return;

        TryBindStoreToMind(uplinkUid, mindId, store);
        var profile = EnsureProfile(mindId, owner);
        var rep = store.Balance.TryGetValue(ContractorCurrency, out var balance) ? balance : FixedPoint2.Zero;
        var offers = profile.Offers.Select(ToUiData).ToArray();
        var active = profile.ActiveOffer == null ? EmptyActiveOffer() : ToUiData(profile.ActiveOffer);

        var requisitions = Requisitions
            .Select(requisition => new ContractorRequisitionData(
                requisition.Id,
                Loc.GetString($"contractor-requisition-{requisition.Id}-name"),
                Loc.GetString($"contractor-requisition-{requisition.Id}-desc"),
                requisition.Cost,
                rep >= requisition.Cost))
            .ToArray();

        var status = profile.ActiveOffer == null
            ? Loc.GetString("contractor-status-idle", ("count", offers.Length))
            : BuildStatus(profile, offers.Length);

        _ui.SetUiState(uplinkUid, ContractorUplinkUiKey.Key, new ContractorUplinkBoundUserInterfaceState(
            rep,
            status,
            offers,
            profile.ActiveOffer != null,
            active,
            profile.Stage,
            requisitions,
            profile.Stage == ContractorContractStage.PortalReady && profile.Portal == null && profile.FalsefireFlare == null));
    }

    private ContractorContractOfferData ToUiData(ContractOffer offer)
    {
        return new ContractorContractOfferData(
            offer.Id,
            offer.TargetName,
            offer.Job,
            offer.Options.Select(option => new ContractorContractOptionData(
                option.Difficulty,
                option.BeaconLabel,
                option.Reason,
                option.AlivePayout,
                option.DeadPayout)).ToArray());
    }

    private ContractorActiveContractData ToUiData(ActiveContract offer)
    {
        return new ContractorActiveContractData(
            offer.Id,
            offer.TargetName,
            offer.Job,
            offer.BeaconLabel,
            offer.Reason,
            offer.Difficulty,
            offer.AlivePayout,
            offer.DeadPayout);
    }

    private static ContractorActiveContractData EmptyActiveOffer()
    {
        return new ContractorActiveContractData(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, ContractorDifficulty.Easy, FixedPoint2.Zero, FixedPoint2.Zero);
    }

    private string BuildStatus(ContractorProfile profile, int offerCount)
    {
        if (profile.ActiveOffer == null)
            return Loc.GetString("contractor-status-idle", ("count", offerCount));

        return profile.Stage switch
        {
            ContractorContractStage.AwaitingBeacon => Loc.GetString("contractor-status-awaiting-beacon", ("location", profile.ActiveOffer.BeaconLabel)),
            ContractorContractStage.PortalReady when profile.Portal != null => Loc.GetString("contractor-status-portal-open"),
            ContractorContractStage.PortalReady when profile.FalsefireFlare != null && profile.FalsefireIgnitionAt != null => Loc.GetString("contractor-status-falsefire-arming"),
            ContractorContractStage.PortalReady when profile.FalsefireFlare != null => Loc.GetString("contractor-status-falsefire-issued"),
            ContractorContractStage.PortalReady => Loc.GetString("contractor-status-portal-ready"),
            _ => Loc.GetString("contractor-status-idle", ("count", offerCount)),
        };
    }

    private bool TryPayTelecrystals(EntityUid contractor, FixedPoint2 amount)
    {
        var uplink = _uplink.FindUplinkTarget(contractor);
        if (uplink == null || !TryComp<StoreComponent>(uplink, out var store))
            return false;

        return _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
        {
            [UplinkSystem.TelecrystalCurrencyPrototype] = amount,
        }, uplink.Value, store);
    }

    private bool TryPayReputation(EntityUid owner, EntityUid mindId, FixedPoint2 amount)
    {
        if (!TryFindContractorStore(owner, mindId, out var storeUid, out var store))
            return false;

        return _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
        {
            [ContractorCurrency] = amount,
        }, storeUid, store);
    }

    private void StripTargetIntoLocker(EntityUid target, EntityUid locker)
    {
        var lockerCoords = Transform(locker).Coordinates;

        if (TryComp<InventoryComponent>(target, out var inventory) && _inventory.TryGetSlots(target, out var slots))
        {
            foreach (var slot in slots)
            {
                if (!_inventory.TryGetSlotEntity(target, slot.Name, out var item, inventory))
                    continue;

                if (!_inventory.TryUnequip(target, slot.Name, true, true, false, inventory))
                    continue;

                _transform.SetCoordinates(item.Value, lockerCoords);
                _entityStorage.Insert(item.Value, locker);
            }
        }

        if (TryComp<HandsComponent>(target, out var hands))
        {
            foreach (var held in _hands.EnumerateHeld((target, hands)).ToArray())
            {
                if (!_hands.TryDrop((target, hands), held, lockerCoords, checkActionBlocker: false, doDropInteraction: false))
                    continue;

                _entityStorage.Insert(held, locker);
            }
        }
    }

    private void EquipPrisonOutfit(EntityUid target)
    {
        if (!TryComp<InventoryComponent>(target, out var inventory))
            return;

        var coords = Transform(target).Coordinates;
        var jumpsuit = Spawn(PrisonUniform, coords);
        var shoes = Spawn(PrisonShoes, coords);
        _inventory.TryEquip(target, jumpsuit, "jumpsuit", silent: true, force: true, inventory: inventory);
        _inventory.TryEquip(target, shoes, "shoes", silent: true, force: true, inventory: inventory);
    }

    private bool TryLoadPirateMap(out EntityUid gridUid)
    {
        if (_pirateGridUid != null && Exists(_pirateGridUid.Value))
        {
            gridUid = _pirateGridUid.Value;
            return true;
        }

        var options = DeserializationOptions.Default with { InitializeMaps = true, PauseMaps = false };
        if (!_mapLoader.TryLoadGrid(new ResPath(PirateMapPath), out _, out var grid, options))
        {
            gridUid = EntityUid.Invalid;
            return false;
        }

        _pirateGridUid = grid.Value.Owner;
        gridUid = grid.Value.Owner;
        return true;
    }

    private bool TryFindContractorStoreFromMind(EntityUid mindId, out EntityUid storeUid, out StoreComponent? store)
    {
        if (TryGetMindOwner(mindId, out var owner))
            return TryFindContractorStore(owner, mindId, out storeUid, out store);

        storeUid = EntityUid.Invalid;
        store = null;
        return false;
    }

    private bool TryFindContractorStore(EntityUid owner, EntityUid mindId, out EntityUid storeUid, out StoreComponent? store)
    {
        var ownerXform = Transform(owner);
        var ownerPosition = _transform.GetWorldPosition(ownerXform);

        var query = EntityQueryEnumerator<StoreComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var storeComp, out var xform))
        {
            if (!storeComp.CurrencyWhitelist.Contains(ContractorCurrency))
                continue;

            if (storeComp.AccountOwner == mindId)
            {
                storeUid = uid;
                store = storeComp;
                return true;
            }

            if (storeComp.AccountOwner != null)
                continue;

            if (xform.MapID != ownerXform.MapID)
                continue;

            var distance = (_transform.GetWorldPosition(xform) - ownerPosition).Length();
            if (distance > StoreLinkRange)
                continue;

            storeComp.AccountOwner = mindId;
            Dirty(uid, storeComp);

            storeUid = uid;
            store = storeComp;
            return true;
        }

        storeUid = EntityUid.Invalid;
        store = null;
        return false;
    }

    private void TryAssignNearbyContractorStore(EntityUid owner, EntityUid mindId)
    {
        TryFindContractorStore(owner, mindId, out _, out _);
    }

    private void TryBindStoreToMind(EntityUid uplinkUid, EntityUid mindId, StoreComponent? store = null)
    {
        if (!Resolve(uplinkUid, ref store, false))
            return;

        if (store.AccountOwner == mindId)
            return;

        store.AccountOwner = mindId;
        Dirty(uplinkUid, store);
    }

    private void RefreshTraitorUplink(EntityUid owner)
    {
        var uplink = _uplink.FindUplinkTarget(owner);
        if (uplink == null || !TryComp<StoreComponent>(uplink.Value, out var store))
            return;

        _store.UpdateUserInterface(owner, uplink.Value, store);
    }

    private bool TryGetMindOwner(EntityUid mindId, out EntityUid owner)
    {
        owner = EntityUid.Invalid;
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.OwnedEntity is not { } owned)
            return false;

        owner = owned;
        return true;
    }

    public bool HasCompletedAliveExtraction(EntityUid targetMindId)
    {
        return _completedExtractedMinds.Contains(targetMindId);
    }

    private bool IsTargetDead(EntityUid targetEntity)
    {
        return _mobState.IsDead(targetEntity);
    }

    private bool TryGetTargetMind(EntityUid targetEntity, out EntityUid targetMind)
    {
        targetMind = EntityUid.Invalid;
        if (!TryComp<MindContainerComponent>(targetEntity, out var mindContainer) || mindContainer.Mind is not { } mindId)
            return false;

        targetMind = mindId;
        return true;
    }

    private void CleanupPortal(EntityUid portalUid, EntityUid contractorMind, bool updateUi = false)
    {
        if (Exists(portalUid))
            QueueDel(portalUid);

        if (_profiles.TryGetValue(contractorMind, out var profile) && profile.Portal == portalUid)
            profile.Portal = null;

        if (updateUi && TryFindContractorStoreFromMind(contractorMind, out var uplinkUid, out _))
            UpdateUplinkUi(uplinkUid, contractorMind);
    }

    private sealed class ContractorProfile
    {
        public readonly List<ContractOffer> Offers = new();
        public readonly HashSet<EntityUid> BlockedTargets = new();
        public ActiveContract? ActiveOffer;
        public ContractorContractStage Stage = ContractorContractStage.None;
        public EntityUid? FalsefireFlare;
        public TimeSpan? FalsefireIgnitionAt;
        public EntityUid? Portal;
        public bool BeaconAnnounced;
    }

    private sealed record ContractOffer(
        string Id,
        EntityUid TargetEntity,
        string TargetName,
        string Job,
        List<ContractOption> Options);

    private sealed record ContractOption(
        ContractorDifficulty Difficulty,
        EntityUid BeaconUid,
        string BeaconLabel,
        string Reason,
        FixedPoint2 AlivePayout,
        FixedPoint2 DeadPayout);

    private sealed record ActiveContract(
        string Id,
        EntityUid TargetEntity,
        string TargetName,
        string Job,
        EntityUid BeaconUid,
        string BeaconLabel,
        ContractorDifficulty Difficulty,
        string Reason,
        FixedPoint2 AlivePayout,
        FixedPoint2 DeadPayout);

    private sealed record RequisitionDefinition(string Id, string? Prototype, FixedPoint2 Cost);

    private sealed record DetainedTarget(EntityUid Locker, EntityCoordinates ReturnCoordinates, TimeSpan ReleaseAt);
}