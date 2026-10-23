using System.Linq;
using System.Numerics;
using Content.Server.Paper;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Lock;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>
/// /obj/machinery/quantum_server (server/_parent.dm, map_handling.dm, loot.dm, obj_generation.dm, util.dm):
/// загрузка домена, выдача аватаров, приём ящиков на клетках-целях, награды и сброс.
/// </summary>
public sealed partial class QuantumServerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly BitrunningAvatarSystem _avatars = default!;
    [Dependency] private readonly BitrunningDomainLoaderSystem _loader = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly SoundSpecifier ProcessingSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/terminal_processing.ogg");
    private static readonly SoundSpecifier InsertDiscSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
    private static readonly SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/terminal_success.ogg");
    private static readonly SoundSpecifier OffSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/terminal_off.ogg");
    private static readonly SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/Bitrunning/terminal_alert.ogg");
    private static readonly SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    private const string EncryptedCacheProto = "BitrunningEncryptedCache";
    private const string EncryptedCuriosityProto = "BitrunningEncryptedCuriosity";
    private const string DecryptedCacheProto = "BitrunningDecryptedCache";
    private const string DecryptedCuriosityProto = "BitrunningDecryptedCuriosity";
    private const string HololadderProto = "BitrunningHololadder";
    private const string CertificateProto = "BitrunningCertificate";
    private const string SupplyChannel = "Supply";

    private static readonly TimeSpan ScrubDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan GoalCheckInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>ORE_MULTIPLIER_*: руда в ящике награды. Титана в SS14 нет — пласталь.</summary>
    private static readonly (string Proto, float Mult, int MinReward)[] OreRewards =
    {
        ("SteelOre1", 3f, 0),
        ("SpaceQuartz1", 2f, 0),
        ("SilverOre1", 0.7f, 2),
        ("SheetPlasteel1", 0.5f, 2),
        ("PlasmaOre1", 1f, 3),
        ("GoldOre1", 0.6f, 3),
        ("UraniumOre1", 0.4f, 3),
        ("DiamondOre1", 0.3f, 4),
        ("XenoBluespaceCrystal", 0.2f, 4),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<QuantumServerComponent, ComponentShutdown>(OnServerShutdown);
        SubscribeLocalEvent<QuantumServerComponent, PowerChangedEvent>(OnServerPower);
        SubscribeLocalEvent<BitrunningEncryptedCacheComponent, LockToggleAttemptEvent>(OnEncryptedUnlock);
        SubscribeLocalEvent<BitrunningVirtualEntityComponent, MobStateChangedEvent>(OnVirtualMobState);
        SubscribeLocalEvent<BitrunningDomainRodComponent, Content.Server.Imperial.Fishing.ImperialFishingCaughtEvent>(OnDomainFishCaught);

        InitializeConsole();
        InitializeHololadder();
        InitializeThreats();
        InitializeDomainScripts();
        InitializeObjectives();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<QuantumServerComponent>();
        while (query.MoveNext(out var uid, out var server))
        {
            if (server.ScrubAt is { } scrub && now >= scrub)
            {
                server.ScrubAt = null;
                ScrubDomain((uid, server));
            }

            if (server.CoolOffAt is { } cool && now >= cool)
            {
                server.CoolOffAt = null;
                CoolOff((uid, server));
            }

            if (server.DomainGrid != null && now >= server.NextGoalCheck)
            {
                server.NextGoalCheck = now + GoalCheckInterval;
                CheckGoalTiles((uid, server));
                CheckBreaches((uid, server));
                UpdateDomainScripts((uid, server), now);
            }
        }

        UpdateByteforges(now);
        UpdateBorders(now);
    }

    #region Состояние

    public bool IsOperational(EntityUid uid)
    {
        return _power.IsPowered(uid);
    }

    private void UpdateAppearance(Entity<QuantumServerComponent> ent)
    {
        string state;
        if (ent.Comp.Domain == null || !IsOperational(ent))
            state = "qserver";
        else
            state = ent.Comp.Ready ? "qserver_on" : "qserver_off";

        _appearance.SetData(ent, BitrunningVisuals.State, state);

        var lit = ent.Comp.Domain != null && IsOperational(ent);
        _light.SetEnabled(ent, lit);
        if (lit)
            _light.SetColor(ent, ent.Comp.Ready ? Color.FromHex("#6496FA") : Color.FromHex("#FA9632"));
    }

    private void OnServerPower(Entity<QuantumServerComponent> ent, ref PowerChangedEvent args)
    {
        // on_broken: при потере питания все отключаются.
        if (!args.Powered)
            SeverConnections(ent);

        UpdateAppearance(ent);
    }

    private void OnServerShutdown(Entity<QuantumServerComponent> ent, ref ComponentShutdown args)
    {
        SeverConnections(ent);
        if (ent.Comp.Domain != null)
            ScrubDomain(ent);
    }

    /// <summary>cool_off(): сервер снова готов, объявление в канал снабжения.</summary>
    private void CoolOff(Entity<QuantumServerComponent> ent)
    {
        ent.Comp.Ready = true;
        UpdateAppearance(ent);
        _radio.SendRadioMessage(ent, Loc.GetString("bitrunning-server-ready-announcement"), SupplyChannel, ent);
    }

    #endregion

    #region Загрузка

    /// <summary>cold_boot_map(): проверки и загрузка домена.</summary>
    public bool ColdBoot(Entity<QuantumServerComponent> ent, string? domainId, bool random)
    {
        if (!ent.Comp.Ready)
            return false;

        if (domainId == null)
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-server-no-domain"), ent);
            return false;
        }

        if (ent.Comp.Domain != null)
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-server-stop-current"), ent);
            return false;
        }

        if (ent.Comp.Avatars.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-server-clients-connected"), ent);
            return false;
        }

        if (!_proto.TryIndex<BitrunningDomainPrototype>(domainId, out var domain) || ent.Comp.Points < domain.Cost)
            return false;

        ent.Comp.Ready = false;
        _audio.PlayPvs(ProcessingSound, ent);

        var built = _loader.Build(domain);
        ent.Comp.Domain = domain.ID;
        if (built == null || !LoadMapItems(ent, domain, built))
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-server-init-failed"), ent);
            if (built != null)
                ent.Comp.DomainMap = built.Map;
            ScrubDomain(ent);
            ent.Comp.Ready = true;
            UpdateAppearance(ent);
            return false;
        }

        ent.Comp.Randomized = random;
        ent.Comp.Ready = true;
        ent.Comp.StartTime = _timing.CurTime;
        ent.Comp.Points -= domain.Cost;

        var spawnChance = Math.Clamp(ent.Comp.Threat * ent.Comp.GlitchChance, 5f, ent.Comp.ThreatProbMax);
        if (_random.Prob(spawnChance / 100f))
            SetupGlitch(ent);

        _audio.PlayPvs(InsertDiscSound, ent);
        _popup.PopupEntity(Loc.GetString("bitrunning-server-domain-loaded"), ent);
        UpdateAppearance(ent);
        Log.Info($"Квантовый сервер {ToPrettyString(ent)} загрузил домен {domain.ID}{(random ? " (случайный)" : "")}");
        return true;
    }

    /// <summary>load_map_items() + load_mob_segments() + on_template_loaded().</summary>
    private bool LoadMapItems(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain, BitrunningDomainLoaderSystem.BuiltDomain built)
    {
        var server = ent.Comp;
        server.DomainMap = built.Map;
        server.DomainGrid = built.Grid;
        server.SafeTiles = new HashSet<Vector2i>(built.SafeTiles);
        server.SecondaryLootLeft = new Dictionary<EntProtoId, int>(domain.SecondaryLoot);
        server.SecondaryLootGenerated = 0;
        server.MainCratePoints = 0;
        server.MainCrateSpot = null;
        server.CacheByPoints = false;
        server.CacheByBoss = false;
        server.CacheSpawned = false;
        server.CacheFound = false;
        server.CuriositiesTotal = 0;
        server.CuriositiesDelivered = 0;

        var cacheSpots = new List<EntityCoordinates>();
        var curiositySpots = new List<EntityCoordinates>();
        var segmentSpots = new List<EntityCoordinates>();
        var replacers = new List<EntityCoordinates>();

        foreach (var created in built.Created)
        {
            if (TerminatingOrDeleted(created))
                continue;

            var coords = Transform(created).Coordinates;
            if (TryComp<BitrunningLandmarkComponent>(created, out var mark))
            {
                switch (mark.Kind)
                {
                    case BitrunningLandmarkKind.Hololadder:
                        server.ExitSpots.Add(coords);
                        QueueDel(created);
                        break;
                    case BitrunningLandmarkKind.PermanentExit:
                        server.ExitSpots.Add(coords);
                        SpawnHololadder(ent, coords);
                        QueueDel(created);
                        break;
                    case BitrunningLandmarkKind.Goal:
                        // Метка остаётся видимой: туда несут ящик.
                        server.GoalTiles.Add((built.Grid, _loader.TileOf(built.Grid, coords)));
                        break;
                    case BitrunningLandmarkKind.Cache:
                        cacheSpots.Add(coords);
                        QueueDel(created);
                        break;
                    case BitrunningLandmarkKind.Curiosity:
                        curiositySpots.Add(coords);
                        QueueDel(created);
                        break;
                    case BitrunningLandmarkKind.LootSignal:
                        server.MainCrateSpot = coords;
                        QueueDel(created);
                        break;
                    case BitrunningLandmarkKind.MobSegment:
                        segmentSpots.Add(coords);
                        QueueDel(created);
                        break;
                    case BitrunningLandmarkKind.CrateReplacer:
                        replacers.Add(coords);
                        QueueDel(created);
                        break;
                }

                continue;
            }

            if (HasComp<MobStateComponent>(created))
                MakeVirtual(ent, created);

            if (domain.CustomSpawn is { } customSpawn && MetaData(created).EntityPrototype?.ID == customSpawn.Id)
                server.CustomSpawns.Add(created);

            if (domain.FishingPoints > 0 && HasComp<Content.Shared.Imperial.Fishing.ImperialFishingRodComponent>(created))
            {
                var rod = EnsureComp<BitrunningDomainRodComponent>(created);
                rod.Server = ent;
                rod.Points = domain.FishingPoints;
            }
        }

        if (server.ExitSpots.Count == 0)
        {
            Log.Error($"Домен {domain.ID}: нет точек выхода (hololadder_spawn).");
            return false;
        }

        if (server.GoalTiles.Count == 0)
        {
            Log.Error($"Домен {domain.ID}: нет клеток-целей (cache_goal_turf).");
            return false;
        }

        if (cacheSpots.Count > 0)
            Spawn(EncryptedCacheProto, _random.Pick(cacheSpots));
        server.CacheByPoints = server.MainCrateSpot != null && cacheSpots.Count == 0;

        _random.Shuffle(curiositySpots);
        foreach (var spot in curiositySpots)
        {
            if (server.SecondaryLootGenerated >= server.SecondaryLootLeft.Values.Sum())
                break;

            Spawn(EncryptedCuriosityProto, spot);
            server.SecondaryLootGenerated++;
        }
        server.CuriositiesTotal = server.SecondaryLootGenerated;

        LoadMobSegments(ent, domain, segmentSpots);

        foreach (var spot in replacers)
        {
            ReplaceCrate(spot);
        }

        return true;
    }

    /// <summary>make_virtual_megafauna() и кандидаты в глюки.</summary>
    private void MakeVirtual(Entity<QuantumServerComponent> server, EntityUid mob)
    {
        var comp = EnsureComp<BitrunningVirtualEntityComponent>(mob);
        comp.Server = server;
        comp.Megafauna = HasMegafaunaAi(mob);
        if (comp.Megafauna)
            server.Comp.CacheByBoss = true;
        if (!comp.Megafauna)
            server.Comp.MutationCandidates.Add(mob);
    }

    private bool HasMegafaunaAi(EntityUid uid)
    {
        return EntityManager.ComponentFactory.TryGetRegistration("MegafaunaAi", out var reg) && HasComp(uid, reg.Type);
    }

    /// <summary>Мегафауна домена: вместо обычного лута — зашифрованный ящик.</summary>
    private void OnVirtualMobState(Entity<BitrunningVirtualEntityComponent> ent, ref MobStateChangedEvent args)
    {
        if (!ent.Comp.Megafauna || args.NewMobState != Content.Shared.Mobs.MobState.Dead)
            return;

        ent.Comp.Megafauna = false;
        Spawn(EncryptedCacheProto, _transform.GetMoverCoordinates(ent));
        if (TryComp<QuantumServerComponent>(ent.Comp.Server, out var server))
            server.CacheSpawned = true;
    }

    /// <summary>load_mob_segments(): метки получают группы мобов домена.</summary>
    private void LoadMobSegments(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain, List<EntityCoordinates> spots)
    {
        if (domain.MobModules.Count == 0 || spots.Count == 0)
            return;

        var modules = domain.MobModules.ToList();
        _random.Shuffle(modules);
        var index = 0;
        foreach (var spot in spots)
        {
            ProtoId<BitrunningMobSegmentPrototype> id;
            if (domain.UniqueMobModules)
            {
                if (index >= modules.Count)
                    break;
                id = modules[index++];
            }
            else
            {
                id = _random.Pick(modules);
            }

            if (!_proto.TryIndex(id, out var segment))
                continue;

            // spawn_mobs(): шанс на всю группу, exact — весь список, иначе 1..max из перемешанного списка.
            if (!_random.Prob(segment.Probability))
                continue;

            var pool = segment.Mobs.Where(m => _proto.HasIndex<EntityPrototype>(m)).ToList();
            if (pool.Count == 0)
                continue;

            _random.Shuffle(pool);
            var total = segment.Exact ? pool.Count : _random.Next(1, segment.Max + 1);
            for (var i = 0; i < total; i++)
            {
                var mob = pool[i % pool.Count];
                // RANGE_TURFS(2): свободная клетка рядом с меткой.
                var offset = new Vector2(_random.Next(-2, 3), _random.Next(-2, 3));
                var spawned = Spawn(mob, spot.Offset(offset));
                MakeVirtual(ent, spawned);
            }
        }
    }

    /// <summary>crate_replacer: ящик с данными меняется местами со случайным обычным ящиком рядом.</summary>
    private void ReplaceCrate(EntityCoordinates spot)
    {
        EntityUid? encrypted = null;
        var others = new List<EntityUid>();
        foreach (var near in _lookup.GetEntitiesInRange(spot, 12f))
        {
            if (!HasComp<Content.Shared.Storage.Components.EntityStorageComponent>(near))
                continue;

            if (HasComp<BitrunningEncryptedCacheComponent>(near))
                encrypted = near;
            else
                others.Add(near);

            _meta.SetEntityName(near, Loc.GetString("bitrunning-crate-unidentified"));
        }

        if (encrypted is not { } crate || others.Count == 0)
            return;

        _meta.SetEntityDescription(crate, MetaData(crate).EntityDescription + " " + Loc.GetString("bitrunning-crate-is-target"));
        var swap = _random.Pick(others);
        var a = Transform(crate).Coordinates;
        var b = Transform(swap).Coordinates;
        _transform.SetCoordinates(crate, b);
        _transform.SetCoordinates(swap, a);
    }

    #endregion

    #region Сброс

    /// <summary>begin_shutdown(): без клиентов — сразу, иначе оповещение и 20 с у консоли.</summary>
    public void BeginShutdown(Entity<QuantumServerComponent> ent, EntityUid user)
    {
        if (ent.Comp.Domain == null)
            return;

        if (ent.Comp.Avatars.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("bitrunning-server-shutting-down"), ent);
            _audio.PlayPvs(OffSound, ent);
            Reset(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("bitrunning-server-alerting-clients"), ent);
        _audio.PlayPvs(AlertSound, ent);
        _popup.PopupEntity(Loc.GetString("bitrunning-server-user-shutting-down", ("user", user)), user, PopupType.MediumCaution);
        foreach (var avatar in ent.Comp.Avatars)
        {
            _avatars.Alert(avatar, "BitrunningShutdown");
        }

        StartShutdownDoAfter(ent, user);
    }

    /// <summary>reset(): все отключаются, домен зачищается через 15 с, сервер остывает.</summary>
    public void Reset(Entity<QuantumServerComponent> ent, bool fast = false)
    {
        ent.Comp.Ready = false;
        ent.Comp.Complete = false;
        SeverConnections(ent);

        if (fast)
        {
            ScrubDomain(ent);
        }
        else
        {
            NotifySpawnedThreats(ent);
            ent.Comp.ScrubAt = _timing.CurTime + ScrubDelay;
        }

        ent.Comp.CoolOffAt = _timing.CurTime + ent.Comp.CooldownTime * ent.Comp.CapacitorCoefficient;
        ent.Comp.Randomized = false;
        ent.Comp.RetriesSpent = 0;
        UpdateAppearance(ent);
    }

    /// <summary>scrub_vdom(): удаляет карту домена со всем содержимым.</summary>
    public void ScrubDomain(Entity<QuantumServerComponent> ent)
    {
        SeverConnections(ent);
        _avatars.DeleteDomainAvatars(ent);

        foreach (var threat in ent.Comp.SpawnedThreats.Concat(ent.Comp.MutationCandidates))
        {
            if (!TerminatingOrDeleted(threat))
                QueueDel(threat);
        }

        if (ent.Comp.DomainMap is { } map && !TerminatingOrDeleted(map))
            QueueDel(map);

        ent.Comp.Domain = null;
        ent.Comp.DomainMap = null;
        ent.Comp.DomainGrid = null;
        ent.Comp.ExitSpots.Clear();
        ent.Comp.GoalTiles.Clear();
        ent.Comp.Avatars.Clear();
        ent.Comp.SpawnedThreats.Clear();
        ent.Comp.MutationCandidates.Clear();
        ent.Comp.CustomSpawns.Clear();
        ent.Comp.SafeTiles.Clear();
        ent.Comp.CountedDeaths.Clear();
        ent.Comp.MainCratePoints = 0;
        ent.Comp.SecondaryLootGenerated = 0;
        UpdateAppearance(ent);
    }

    /// <summary>sever_connections(): насильный обрыв всех подключений.</summary>
    public void SeverConnections(Entity<QuantumServerComponent> ent)
    {
        foreach (var avatar in ent.Comp.Avatars.ToList())
        {
            _avatars.Disconnect(avatar, causeDamage: true);
        }
    }

    #endregion

    #region Аватары

    /// <summary>get_avatar_destination(): новая голо-лестница на свободной точке выхода.</summary>
    public EntityCoordinates? GetAvatarDestination(Entity<QuantumServerComponent> ent)
    {
        while (ent.Comp.CustomSpawns.Count > 0)
        {
            var custom = _random.Pick(ent.Comp.CustomSpawns);
            if (TerminatingOrDeleted(custom))
            {
                ent.Comp.CustomSpawns.Remove(custom);
                continue;
            }

            var coords = Transform(custom).Coordinates;
            if (_proto.TryIndex(ent.Comp.Domain, out var domain) && !domain.KeepCustomSpawns)
            {
                ent.Comp.CustomSpawns.Remove(custom);
                QueueDel(custom);
            }

            return coords;
        }

        if (ent.Comp.ExitSpots.Count == 0 || ent.Comp.RetriesSpent >= ent.Comp.ExitSpots.Count)
            return null;

        foreach (var spot in ent.Comp.ExitSpots)
        {
            var occupied = false;
            foreach (var near in _lookup.GetEntitiesInRange(spot, 0.3f))
            {
                if (HasComp<BitrunningHololadderComponent>(near))
                {
                    occupied = true;
                    break;
                }
            }

            if (occupied)
                continue;

            SpawnHololadder(ent, spot);
            ent.Comp.RetriesSpent++;
            return spot;
        }

        return null;
    }

    private void SpawnHololadder(Entity<QuantumServerComponent> ent, EntityCoordinates coords)
    {
        var ladder = Spawn(HololadderProto, coords);
        EnsureComp<BitrunningHololadderComponent>(ladder).Server = ent;
    }

    /// <summary>Сервер в радиусе от машины (oview(4)).</summary>
    public Entity<QuantumServerComponent>? FindServer(EntityUid near)
    {
        var coords = Transform(near).Coordinates;
        foreach (var uid in _lookup.GetEntitiesInRange<QuantumServerComponent>(coords, BitrunningConstants.LinkRange))
        {
            return uid;
        }

        return null;
    }

    #endregion

    #region Ящики

    private void OnEncryptedUnlock(Entity<BitrunningEncryptedCacheComponent> ent, ref LockToggleAttemptEvent args)
    {
        // can_unlock(): зашифрованный ящик открывается только расшифровкой в сейфхаусе.
        args.Cancelled = true;
    }

    /// <summary>on_goal_turf_entered(): ящик на клетке-цели превращается в награду у байтфоржа.</summary>
    private void CheckGoalTiles(Entity<QuantumServerComponent> ent)
    {
        var query = EntityQueryEnumerator<BitrunningEncryptedCacheComponent, TransformComponent>();
        var found = new List<(EntityUid, bool)>();
        while (query.MoveNext(out var uid, out var cache, out var xform))
        {
            if (xform.GridUid is not { } grid || grid != ent.Comp.DomainGrid || _container.IsEntityInContainer(uid))
                continue;

            if (ent.Comp.GoalTiles.Contains((grid, _loader.TileOf(grid, xform.Coordinates))))
                found.Add((uid, cache.Main));
        }

        foreach (var (cache, main) in found)
        {
            var forge = GetRandomNearbyForge(ent);
            if (forge == null)
                continue;

            if (main)
                GenerateLoot(ent, cache, forge.Value);
            else
                GenerateSecondaryLoot(ent, cache, forge.Value);
        }
    }

    private EntityUid? GetRandomNearbyForge(Entity<QuantumServerComponent> ent)
    {
        var forges = _lookup.GetEntitiesInRange<ByteforgeComponent>(Transform(ent).Coordinates, BitrunningConstants.LinkRange)
            .Select(f => f.Owner)
            .ToList();
        return forges.Count == 0 ? null : _random.Pick(forges);
    }

    /// <summary>generate_loot().</summary>
    private void GenerateLoot(Entity<QuantumServerComponent> ent, EntityUid cache, EntityUid forge)
    {
        if (!_proto.TryIndex(ent.Comp.Domain, out var domain))
            return;

        // Кто сидел внутри ящика — безопасно выходит (COMSIG_BITRUNNER_CACHE_SEVER).
        if (_container.TryGetContainer(cache, SharedEntityStorageSystem.ContainerName, out var contents))
        {
            foreach (var inside in contents.ContainedEntities.ToList())
            {
                if (HasComp<BitrunningAvatarComponent>(inside))
                    _avatars.Disconnect(inside, causeDamage: false);
            }
        }

        SparkAt(Transform(cache).Coordinates);
        QueueDel(cache);

        var reward = domain.Reward;
        ent.Comp.Points += reward;
        _audio.PlayPvs(SuccessSound, ent);
        foreach (var avatar in ent.Comp.Avatars)
        {
            _avatars.OnDomainCompleted(avatar, reward);
        }

        var bonus = CalculateRewards(ent, domain);
        var elapsed = _timing.CurTime - ent.Comp.StartTime;
        var grade = GradeCompletion(ent, domain, elapsed);

        var crate = Spawn(DecryptedCacheProto, MapCoordinates.Nullspace);
        var certificate = Spawn(CertificateProto, MapCoordinates.Nullspace);
        _paper.SetContent(certificate, GetCertificate(ent, domain, elapsed, grade, bonus));
        _storage.Insert(certificate, crate);
        PopulateReward(crate, domain, reward, bonus);

        StartToSpawn(forge, crate);
        ent.Comp.Complete = true;
        Log.Info($"Домен {domain.ID} пройден на {grade}, бонус {bonus:0.##}");
    }

    /// <summary>generate_secondary_loot().</summary>
    private void GenerateSecondaryLoot(Entity<QuantumServerComponent> ent, EntityUid curiosity, EntityUid forge)
    {
        ent.Comp.CuriositiesDelivered++;
        SparkAt(Transform(curiosity).Coordinates);
        QueueDel(curiosity);

        var box = Spawn(DecryptedCuriosityProto, MapCoordinates.Nullspace);
        if (PickSecondaryLoot(ent) is { } loot)
            _storage.Insert(Spawn(loot, MapCoordinates.Nullspace), box);

        StartToSpawn(forge, box);
    }

    /// <summary>SSbitrunning.pick_secondary_loot(): взвешенный выбор с убыванием.</summary>
    private EntProtoId? PickSecondaryLoot(Entity<QuantumServerComponent> ent)
    {
        var total = ent.Comp.SecondaryLootLeft.Values.Sum();
        if (total <= 0)
            return null;

        var roll = _random.Next(total);
        foreach (var (proto, weight) in ent.Comp.SecondaryLootLeft.ToList())
        {
            if (roll < weight)
            {
                ent.Comp.SecondaryLootLeft[proto] = weight - 1;
                return proto;
            }

            roll -= weight;
        }

        return null;
    }

    /// <summary>decrypted/PopulateContents(): руда по очкам домена и completion_loot.</summary>
    private void PopulateReward(EntityUid crate, BitrunningDomainPrototype domain, int reward, float multiplier)
    {
        foreach (var (proto, amount) in domain.CompletionLoot)
        {
            for (var i = 0; i < amount; i++)
            {
                _storage.Insert(Spawn(proto, MapCoordinates.Nullspace), crate);
            }
        }

        foreach (var (proto, mult, min) in OreRewards)
        {
            if (reward <= min)
                continue;

            var count = Math.Max((int) MathF.Ceiling((_random.NextFloat() + 0.5f) * (multiplier + reward) * mult), 1);
            var stack = Spawn(proto, MapCoordinates.Nullspace);
            _stack.SetCount((stack, null), count);
            _storage.Insert(stack, crate);
        }
    }

    /// <summary>calculate_rewards().</summary>
    private float CalculateRewards(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain)
    {
        var bonus = 0.8f;
        if (ent.Comp.Randomized)
            bonus += 0.2f;
        bonus += ent.Comp.ServoBonus;
        bonus += ent.Comp.SpawnedThreats.Count * 2;
        bonus += GetMultiplayerBonus(ent);
        bonus += GetNohitBonus(ent, domain);
        return bonus;
    }

    private float GetMultiplayerBonus(Entity<QuantumServerComponent> ent)
    {
        return Math.Max(0, ent.Comp.Avatars.Count - 1) * ent.Comp.MultiplayerBonus;
    }

    private float GetNohitBonus(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain)
    {
        if ((domain.Flags & BitrunningDomainFlags.NoNohitBonus) != 0)
            return 0f;

        return ent.Comp.Avatars.Count(a => TryComp<BitrunningAvatarComponent>(a, out var avatar) && avatar.Nohit) * ent.Comp.NohitBonus;
    }

    /// <summary>grade_completion(): оценка и рост угрозы.</summary>
    private string GradeCompletion(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain, TimeSpan elapsed)
    {
        var score = ent.Comp.SpawnedThreats.Count * 5 + domain.Reward;
        var timeScore = elapsed.TotalMinutes switch
        {
            <= 1 => 10,
            <= 2 => 5,
            <= 5 => 3,
            <= 10 => 2,
            _ => 1,
        };

        score += timeScore * ((int) domain.Difficulty + 1);
        ent.Comp.Threat += score;

        return score switch
        {
            <= 4 => "D",
            <= 7 => "C",
            <= 10 => "B",
            <= 13 => "A",
            _ => "S",
        };
    }

    /// <summary>get_completion_certificate().</summary>
    private string GetCertificate(Entity<QuantumServerComponent> ent, BitrunningDomainPrototype domain, TimeSpan elapsed, string grade, float bonus)
    {
        var basePoints = domain.Reward - (ent.Comp.Randomized ? 1 : 0);
        var lines = new List<string>
        {
            Loc.GetString("bitrunning-certificate-title"),
            "",
            Loc.GetString("bitrunning-certificate-domain", ("domain", Loc.GetString(domain.Name)), ("random", ent.Comp.Randomized ? 1 : 0)),
            Loc.GetString("bitrunning-certificate-difficulty", ("difficulty", (int) domain.Difficulty)),
            Loc.GetString("bitrunning-certificate-threats", ("threats", ent.Comp.SpawnedThreats.Count)),
            Loc.GetString("bitrunning-certificate-base", ("points", basePoints), ("random", ent.Comp.Randomized ? 1 : 0)),
            Loc.GetString("bitrunning-certificate-bonus", ("bonus", bonus.ToString("0.##"))),
        };

        if (bonus > 1)
        {
            lines.Add("");
            lines.Add(Loc.GetString("bitrunning-certificate-bonuses"));
            if (ent.Comp.Randomized)
                lines.Add(Loc.GetString("bitrunning-certificate-bonus-random"));
            if (GetMultiplayerBonus(ent) is var mp and > 0)
                lines.Add(Loc.GetString("bitrunning-certificate-bonus-multiplayer", ("bonus", mp.ToString("0.##"))));
            if (GetNohitBonus(ent, domain) is var nohit and > 0)
                lines.Add(Loc.GetString("bitrunning-certificate-bonus-nohit", ("bonus", nohit.ToString("0.##"))));
            if (ent.Comp.SpawnedThreats.Count > 0)
                lines.Add(Loc.GetString("bitrunning-certificate-bonus-threats", ("bonus", ent.Comp.SpawnedThreats.Count * 2)));
            if (ent.Comp.ServoBonus > 0.2f)
                lines.Add(Loc.GetString("bitrunning-certificate-bonus-parts", ("bonus", ent.Comp.ServoBonus.ToString("0.##"))));
        }

        lines.Add("");
        lines.Add(Loc.GetString("bitrunning-certificate-time", ("time", $"{(int) elapsed.TotalMinutes}:{elapsed.Seconds:00}")));
        lines.Add(Loc.GetString("bitrunning-certificate-grade", ("grade", grade)));
        return string.Join("\n", lines);
    }

    private void OnDomainFishCaught(Entity<BitrunningDomainRodComponent> ent, ref Content.Server.Imperial.Fishing.ImperialFishingCaughtEvent args)
    {
        if (args.Reward == null || !TryComp<QuantumServerComponent>(ent.Comp.Server, out var server))
            return;

        AddCratePoints((ent.Comp.Server, server), ent.Comp.Points);
    }

    /// <summary>Очки на картах с main_crate_point_goal: ящик появляется на loot_signal.</summary>
    public void AddCratePoints(Entity<QuantumServerComponent> ent, float amount = 1)
    {
        if (!_proto.TryIndex(ent.Comp.Domain, out var domain))
            return;

        ent.Comp.MainCratePoints += amount;
        if (ent.Comp.MainCratePoints < domain.CratePointGoal || ent.Comp.MainCrateSpot is not { } spot)
            return;

        _audio.PlayPvs(BlinkSound, spot);
        Spawn(EncryptedCacheProto, spot);
        ent.Comp.CacheSpawned = true;
        SparkAt(spot);
        ent.Comp.MainCrateSpot = null;
    }

    private void SparkAt(EntityCoordinates coords)
    {
        _audio.PlayPvs(BlinkSound, coords);
        Spawn("EffectSparks", coords);
    }

    #endregion
}
