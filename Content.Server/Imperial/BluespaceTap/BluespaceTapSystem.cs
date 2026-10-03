using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.BluespaceTap;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BluespaceTap;

/// <summary>Товар блюспейс-сборщика (bluespace_tap_product).</summary>
[DataDefinition]
public sealed partial class BluespaceTapProduct
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Common;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Uncommon;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Rare;

    /// <summary>Стоимость в очках; после каждой покупки растёт в 1.2 раза.</summary>
    [DataField(required: true)]
    public int Cost;

    /// <summary>Каждые сколько очков товар появляется сам (clothing_interval и т. д.).</summary>
    [DataField(required: true)]
    public float Interval;

    [DataField(required: true)]
    public LocId ProducedMessage;
}

/// <summary>
/// Блюспейс-сборщик (obj/machinery/power/bluespace_tap из Paradise): берёт мощность прямо из ВВ-сети,
/// копит очки и вытягивает из других измерений предметы.
/// </summary>
[RegisterComponent]
public sealed partial class BluespaceTapComponent : Component
{
    [DataField(required: true)]
    public List<BluespaceTapProduct> Products = new();

    /// <summary>Порог «материнской жилы» (motherlode_interval, цель станции).</summary>
    [DataField]
    public float MotherlodeInterval = 45000;

    [DataField]
    public TimeSpan ProcessInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public EntProtoId Portal = "ImperialNetherPortalTap";

    [DataField]
    public EntProtoId SpawnEffect = "ImperialBluespaceTapPortalEffect";

    [DataField]
    public EntProtoId FlashEffect = "ImperialBluespaceTapFlash";

    [DataField]
    public EntProtoId RadiationPulse = "ImperialBluespaceTapRadiation";

    [DataField]
    public SoundSpecifier BlinkSound = new SoundPathSpecifier("/Audio/Magic/blink.ogg");

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Imperial/BluespaceTap/harvester.ogg");

    [DataField]
    public SoundSpecifier ZapSound = new SoundPathSpecifier("/Audio/Imperial/BluespaceTap/eleczap.ogg");

    [DataField]
    public string RadioChannel = "Engineering";

    [ViewVariables] public float MiningPower;
    [ViewVariables] public float DesiredMiningPower;
    [ViewVariables] public float MinedPoints;
    [ViewVariables] public float Points;
    [ViewVariables] public float TotalPoints;
    [ViewVariables] public float AvailablePower;
    [ViewVariables] public bool AutoShutdown = true;
    [ViewVariables] public bool Stabilizers = true;
    [ViewVariables] public float StabilizerPower;
    [ViewVariables] public bool StabilizerPriority = true;
    [ViewVariables] public HashSet<EntityUid> ActivePortals = new();
    [ViewVariables] public int Spawning;
    [ViewVariables] public bool IsDirty;
    [ViewVariables] public TimeSpan NextProcess;

    /// <summary>Следующие пороги автоматической выдачи по каждому товару.</summary>
    [ViewVariables] public List<float> NextIntervals = new();

    [ViewVariables] public float NextMotherlode;

    /// <summary>Отложенные порталы: время появления.</summary>
    [ViewVariables] public List<TimeSpan> PendingPortals = new();

    [ViewVariables] public float LastDraw;
}

/// <summary>Связь с иным миром (spawner/nether/bluespace_tap): выпускает тварей и затягивает касающихся.</summary>
[RegisterComponent]
public sealed partial class NetherPortalComponent : Component
{
    public const string ContainerId = "nether";

    [DataField]
    public List<EntProtoId> MobTypes = new() { "ImperialNetherMigo", "ImperialNetherCreature", "ImperialNetherBlankBody" };

    [DataField]
    public EntProtoId BlankBody = "ImperialNetherBlankBody";

    [DataField]
    public TimeSpan SpawnTime = TimeSpan.FromSeconds(30);

    [DataField]
    public int MaxMobs = 5;

    [DataField]
    public DamageSpecifier ConsumeDamage = new() { DamageDict = new() { ["Blunt"] = 60 } };

    [DataField]
    public SoundSpecifier ConsumeSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    [ViewVariables] public EntityUid? Source;
    [ViewVariables] public HashSet<EntityUid> Spawned = new();
    [ViewVariables] public TimeSpan NextSpawn;
    [ViewVariables] public TimeSpan NextConsume;
}

/// <summary>Пустое тело: при смерти отпускает поглощённого порталом.</summary>
[RegisterComponent]
public sealed partial class NetherBlankBodyComponent : Component
{
    public const string ContainerId = "held_body";
}

public sealed class BluespaceTapSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly PowerNetSystem _powerNet = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const float KW = 1000f;
    private const float MW = 1000000f;

    /// <summary>POINTS_PER_W.</summary>
    private const float PointsPerW = 4e-6f;

    /// <summary>BASE_POINTS: очков за каждые 50 кВт первых 500 кВт.</summary>
    private const float BasePoints = 2;

    /// <summary>PROB_CAP и PROB_CURVE шанса событий.</summary>
    private const float ProbCap = 5;
    private const float ProbCurve = 250;

    /// <summary>Цель станции (station_goal/bluespace_tap.goal).</summary>
    public const float Goal = 45000;

    /// <summary>static product_list: цены растут общие для всех сборщиков.</summary>
    private readonly Dictionary<int, int> _costs = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _costs.Clear());
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);

        SubscribeLocalEvent<BluespaceTapComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BluespaceTapComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BluespaceTapComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BluespaceTapComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<BluespaceTapComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateUi((uid, comp)));
        SubscribeLocalEvent<BluespaceTapComponent, BluespaceTapSetPowerMessage>(OnSetPower);
        SubscribeLocalEvent<BluespaceTapComponent, BluespaceTapVendMessage>(OnVend);
        SubscribeLocalEvent<BluespaceTapComponent, BluespaceTapToggleAutoShutdownMessage>(OnToggleAutoShutdown);
        SubscribeLocalEvent<BluespaceTapComponent, BluespaceTapToggleStabilizersMessage>(OnToggleStabilizers);
        SubscribeLocalEvent<BluespaceTapComponent, BluespaceTapToggleStabilizerPriorityMessage>(OnTogglePriority);

        SubscribeLocalEvent<NetherPortalComponent, InteractHandEvent>(OnPortalInteractHand);
        SubscribeLocalEvent<NetherPortalComponent, ComponentShutdown>(OnPortalShutdown);
        SubscribeLocalEvent<NetherBlankBodyComponent, MobStateChangedEvent>(OnBlankBodyState);
    }

    private static float NearestMW(float power)
    {
        return power - power % MW;
    }

    #region Цель и рекорд

    private void OnRoundEnd(RoundEndTextAppendEvent ev)
    {
        var highscore = -1f;
        var query = EntityQueryEnumerator<BluespaceTapComponent>();
        while (query.MoveNext(out _, out var tap))
        {
            highscore = Math.Max(highscore, tap.TotalPoints);
        }

        if (highscore < 0)
            return;

        ev.AddLine(Loc.GetString(highscore >= Goal ? "bluespace-tap-highscore-success" : "bluespace-tap-highscore",
            ("points", (int) highscore)));
    }

    #endregion

    #region Машина

    private void OnMapInit(Entity<BluespaceTapComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextIntervals = ent.Comp.Products.Select(p => p.Interval).ToList();
        ent.Comp.NextMotherlode = ent.Comp.MotherlodeInterval;
        ent.Comp.NextProcess = _timing.CurTime + ent.Comp.ProcessInterval;
        UpdateVisuals(ent);
    }

    private void OnExamined(Entity<BluespaceTapComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("bluespace-tap-examine"));
        if (ent.Comp.IsDirty)
            args.PushMarkup(Loc.GetString("bluespace-tap-examine-dirty"));
    }

    /// <summary>cleaning_act: мыло, швабра или тряпка счищают грязь.</summary>
    private void OnInteractUsing(Entity<BluespaceTapComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.IsDirty)
            return;

        var isSoap = MetaData(args.Used).EntityPrototype?.ID.Contains("Soap") == true;
        if (!isSoap && !HasComp<AbsorbentComponent>(args.Used))
            return;

        args.Handled = true;
        ent.Comp.IsDirty = false;
        _popup.PopupEntity(Loc.GetString("bluespace-tap-cleaned"), ent, args.User);
        UpdateVisuals(ent);
        UpdateUi(ent);
    }

    private void OnEmagged(Entity<BluespaceTapComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || _emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
        Spawn("EffectSparks", Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("bluespace-tap-emagged", ("user", args.UserUid)), ent, PopupType.MediumCaution);
    }

    private bool IsEmagged(EntityUid uid)
    {
        return _emag.CheckFlag(uid, EmagType.Interaction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BluespaceTapComponent, PowerConsumerComponent>();
        while (query.MoveNext(out var uid, out var tap, out var consumer))
        {
            for (var i = tap.PendingPortals.Count - 1; i >= 0; i--)
            {
                if (now < tap.PendingPortals[i])
                    continue;

                tap.PendingPortals.RemoveAt(i);
                SpawnPortal((uid, tap));
            }

            if (now < tap.NextProcess)
                continue;

            tap.NextProcess = now + tap.ProcessInterval;
            Process((uid, tap), consumer);
        }

        var portals = EntityQueryEnumerator<NetherPortalComponent>();
        while (portals.MoveNext(out var uid, out var portal))
        {
            UpdatePortal((uid, portal), now);
        }
    }

    /// <summary>get_surplus: сколько свободной мощности в сети без учёта нас самих.</summary>
    private float GetSurplus(PowerConsumerComponent consumer)
    {
        if (consumer.Net is not { } net)
            return 0;

        var stats = _powerNet.GetNetworkStatistics(net.NetworkNode);
        return Math.Max(0, stats.SupplyTheoretical - (stats.Consumption - consumer.DrawRate));
    }

    /// <summary>bluespace_tap/process.</summary>
    private void Process(Entity<BluespaceTapComponent> ent, PowerConsumerComponent consumer)
    {
        var tap = ent.Comp;
        var emagged = IsEmagged(ent);

        // Если сеть не дала запрошенного, добыча пропорционально падает.
        var deliveredRatio = tap.LastDraw > 0 ? Math.Clamp(consumer.ReceivedPower / tap.LastDraw, 0f, 1f) : 1f;

        tap.AvailablePower = GetSurplus(consumer);
        var mining = tap.AvailablePower;
        if (mining > MW)
            mining = NearestMW(mining);

        if (emagged)
        {
            tap.DesiredMiningPower = mining;
            tap.StabilizerPower = 0;
        }
        else if (tap.Stabilizers)
        {
            if (tap.StabilizerPriority)
            {
                tap.StabilizerPower = Math.Min(
                    Math.Max(mining - Math.Max(NearestMW(mining / 2), NearestMW((mining + 30 * MW) / 3)), 0),
                    Math.Clamp(tap.DesiredMiningPower - Math.Clamp(30 * MW - tap.DesiredMiningPower, 0, 15 * MW), 0, tap.DesiredMiningPower));
                mining -= tap.StabilizerPower;
            }
            else
            {
                tap.StabilizerPower = Math.Clamp(mining - tap.DesiredMiningPower, 0,
                    Math.Max(0, tap.DesiredMiningPower - Math.Clamp(30 * MW - tap.DesiredMiningPower, 0, 15 * MW)));
            }
        }
        else
        {
            tap.StabilizerPower = 0;
        }

        tap.MiningPower = Math.Min(mining, tap.DesiredMiningPower);

        // consume_direct_power: берём мощность прямо из ВВ-сети.
        consumer.DrawRate = tap.MiningPower + tap.StabilizerPower;
        tap.LastDraw = consumer.DrawRate;

        var effective = tap.MiningPower * deliveredRatio;
        if (!tap.IsDirty)
        {
            tap.MinedPoints = Math.Min(BasePoints * (effective / (50 * KW)), 20) + effective * (PointsPerW + (emagged ? 1 : 0) / MW);
            tap.Points += tap.MinedPoints;
            tap.TotalPoints += tap.MinedPoints;
        }

        for (var i = 0; i < tap.Products.Count; i++)
        {
            if (tap.TotalPoints <= tap.NextIntervals[i])
                continue;

            Produce(ent, i, purchased: false, doubleChance: !tap.Stabilizers);
            Radio(ent, Loc.GetString(tap.Products[i].ProducedMessage));
            tap.NextIntervals[i] += tap.Products[i].Interval;
        }

        if (tap.TotalPoints > tap.NextMotherlode)
        {
            ProduceMotherlode(ent);
            tap.NextMotherlode += tap.MotherlodeInterval;
        }

        // Шанс порталов: 0.1 % за цикл на каждый мегаватт превышения над стабилизаторами (+5 % под емагом).
        var portalChance = (tap.MiningPower - Math.Clamp(30 * MW - tap.MiningPower, 0, 15 * MW) - tap.StabilizerPower) / (10 * MW)
            + (emagged ? 5 : 0);
        if (portalChance > 0 && _random.Prob(Math.Min(portalChance / 100f, 1f)))
            StartIncursion(ent, emagged);

        TryEvents(ent, emagged);
        UpdateVisuals(ent);
        UpdateUi(ent);
    }

    private void StartIncursion(Entity<BluespaceTapComponent> ent, bool emagged)
    {
        var tap = ent.Comp;
        if (tap.Spawning == 0 || tap.ActivePortals.Count == 0)
        {
            var location = _navMap.GetNearestBeaconString(ent.Owner, onlyName: true);
            var tail = emagged ? "bluespace-tap-incursion-emagged"
                : tap.AutoShutdown ? "bluespace-tap-incursion-shutdown"
                : "bluespace-tap-incursion-no-shutdown";
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("bluespace-tap-incursion", ("location", location), ("tail", Loc.GetString(tail))),
                Loc.GetString("bluespace-tap-incursion-sender"),
                announcementSound: tap.AlertSound,
                colorOverride: Color.Red);
        }

        if (!emagged && tap.AutoShutdown)
            tap.DesiredMiningPower = 0;

        // Лишний портал на каждые 30 МВт выше 15.
        var amount = _random.Next(1, 4) + (int) Math.Round(Math.Max((tap.MiningPower - 15 * MW) / (30 * MW), 0));
        tap.Spawning += amount;
        var delay = TimeSpan.Zero;
        for (var i = 0; i < amount; i++)
        {
            tap.PendingPortals.Add(_timing.CurTime + delay);
            delay += TimeSpan.FromSeconds(_random.Next(3, 6));
        }
    }

    private void SpawnPortal(Entity<BluespaceTapComponent> ent)
    {
        var tap = ent.Comp;
        tap.Spawning = Math.Max(0, tap.Spawning - 1);

        var coords = Transform(ent).Coordinates.Offset(new System.Numerics.Vector2(_random.Next(-5, 6), _random.Next(-5, 6)));
        var portal = Spawn(tap.Portal, coords);
        var comp = EnsureComp<NetherPortalComponent>(portal);
        comp.Source = ent;
        // Лишняя тварь на каждые 20 МВт выше 15 МВт.
        comp.MaxMobs = 5 + (int) Math.Max((tap.MiningPower - 15 * MW) / (20 * MW), 0);
        tap.ActivePortals.Add(portal);
        UpdateVisuals(ent);
    }

    private void Radio(EntityUid tap, string message)
    {
        _radio.SendRadioMessage(tap, message, Comp<BluespaceTapComponent>(tap).RadioChannel, tap);
    }

    #endregion

    #region Выдача предметов

    private int GetCost(BluespaceTapComponent tap, int index)
    {
        return _costs.TryGetValue(index, out var cost) ? cost : tap.Products[index].Cost;
    }

    /// <summary>produce: товар появляется рядом; при double_chance с шансом 25 % ещё один — где-то на станции.</summary>
    private void Produce(Entity<BluespaceTapComponent> ent, int index, bool purchased, bool doubleChance)
    {
        var tap = ent.Comp;
        if (index < 0 || index >= tap.Products.Count)
            return;

        if (purchased)
        {
            var cost = GetCost(tap, index);
            if (cost > tap.Points)
                return;

            tap.Points -= cost;
            _costs[index] = (int) Math.Round(1.2 * cost);
        }

        _audio.PlayPvs(tap.BlinkSound, ent);
        Spawn("EffectSparks", Transform(ent).Coordinates);

        SpawnItem(ent, tap.Products[index], FindSpawnLocation(ent, random: false) ?? Transform(ent).Coordinates);

        if (doubleChance && _random.Prob(0.25f) && FindSpawnLocation(ent, random: true) is { } far)
            SpawnItem(ent, tap.Products[index], far);
    }

    /// <summary>produce_motherlode: каждый товар по 5 раз у машины и по станции.</summary>
    private void ProduceMotherlode(Entity<BluespaceTapComponent> ent)
    {
        Radio(ent, Loc.GetString("bluespace-tap-motherlode"));
        foreach (var product in ent.Comp.Products)
        {
            for (var i = 0; i < 5; i++)
            {
                SpawnItem(ent, product, FindSpawnLocation(ent, random: false) ?? Transform(ent).Coordinates);
                if (FindSpawnLocation(ent, random: true) is { } far)
                    SpawnItem(ent, product, far);
            }
        }
    }

    /// <summary>spawn_item: обычное 60 %, необычное 30 %, редкое 10 %.</summary>
    private void SpawnItem(Entity<BluespaceTapComponent> ent, BluespaceTapProduct product, EntityCoordinates coords)
    {
        var roll = _random.NextFloat();
        var table = roll < 0.6f ? product.Common : roll < 0.9f ? product.Uncommon : product.Rare;

        Spawn(ent.Comp.SpawnEffect, coords);
        _audio.PlayPvs(ent.Comp.BlinkSound, ent);
        foreach (var proto in _entityTable.GetSpawns(_proto.Index(table)))
        {
            Spawn(proto, coords);
        }

        Spawn(ent.Comp.FlashEffect, Transform(ent).Coordinates);
    }

    /// <summary>find_spawn_location: свободная клетка в радиусе 3 или случайная по станции.</summary>
    private EntityCoordinates? FindSpawnLocation(EntityUid tap, bool random)
    {
        var xform = Transform(tap);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var bounds = gridComp.LocalAABB;
        for (var attempt = 0; attempt < (random ? 100 : 30); attempt++)
        {
            Vector2i indices;
            if (random)
            {
                indices = new Vector2i(
                    (int) Math.Floor(_random.NextFloat(bounds.Left, bounds.Right)),
                    (int) Math.Floor(_random.NextFloat(bounds.Bottom, bounds.Top)));
            }
            else
            {
                indices = center + new Vector2i(_random.Next(-3, 4), _random.Next(-3, 4));
            }

            if (!_map.TryGetTileRef(grid, gridComp, indices, out var tile)
                || _turf.IsSpace(tile)
                || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
            {
                continue;
            }

            return _map.GridTileToLocal(grid, gridComp, indices);
        }

        return null;
    }

    #endregion

    #region События

    /// <summary>try_events: грязь, электрическая дуга, радиация или выброс газа.</summary>
    private void TryEvents(Entity<BluespaceTapComponent> ent, bool emagged)
    {
        if (ent.Comp.MiningPower <= 0)
            return;

        var megawatts = ent.Comp.MiningPower / MW;
        var chance = ProbCap * megawatts / (megawatts + ProbCurve) + (emagged ? 5 : 0);
        if (!_random.Prob(Math.Min(chance / 100f, 1f)))
            return;

        switch (_random.Next(4))
        {
            case 0:
                EventDirty(ent);
                break;
            case 1:
                EventArc(ent);
                break;
            case 2:
                EventRadiation(ent);
                break;
            default:
                EventGas(ent);
                break;
        }
    }

    private void EventDirty(Entity<BluespaceTapComponent> ent)
    {
        ent.Comp.IsDirty = true;
        var solution = new Solution();
        solution.AddReagent(_random.Pick(new[] { "Carbon", "Flour", "Blood" }), 50);

        var smoke = Spawn("Smoke", Transform(ent).Coordinates);
        _smoke.StartSmoke(smoke, solution, 10f, 3);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/smoke.ogg"), ent);
        Radio(ent, Loc.GetString("bluespace-tap-event-dirty"));
    }

    private void EventArc(Entity<BluespaceTapComponent> ent)
    {
        var mobs = _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, 5f)
            .Where(m => !_mobState.IsDead(m))
            .Select(m => m.Owner)
            .ToList();

        var mass = _random.Prob(0.5f);
        Radio(ent, Loc.GetString("bluespace-tap-event-arc", ("class", mass ? "E-2" : "E-1")));
        if (mobs.Count == 0)
            return;

        foreach (var mob in mass ? mobs : new List<EntityUid> { _random.Pick(mobs) })
        {
            _electrocution.TryDoElectrocution(mob, ent, _random.Next(5, 26), TimeSpan.FromSeconds(1), true);
            _audio.PlayPvs(ent.Comp.ZapSound, mob);
        }
    }

    private void EventRadiation(Entity<BluespaceTapComponent> ent)
    {
        // Сильная радиация чуть больше двух минут.
        SpawnAttachedTo(ent.Comp.RadiationPulse, Transform(ent).Coordinates);
        Radio(ent, Loc.GetString("bluespace-tap-event-radiation"));
    }

    private void EventGas(Entity<BluespaceTapComponent> ent)
    {
        var options = new (string Class, Gas Gas)[]
        {
            ("G-1", Gas.Nitrogen),
            ("G-2", Gas.Oxygen),
            ("G-3", Gas.NitrousOxide),
            ("G-4", Gas.CarbonDioxide),
            ("G-5", Gas.Plasma),
            ("G-6", Gas.Frezon),
            ("G-7", Gas.Hydrogen),
            ("G-8", Gas.WaterVapor),
        };
        var (gasClass, gas) = _random.Pick(options);

        if (_atmos.GetContainingMixture(ent.Owner, excite: true) is { } mixture)
            mixture.AdjustMoles(gas, gas == Gas.NitrousOxide ? 200 : 250);

        Radio(ent, Loc.GetString("bluespace-tap-event-gas", ("class", gasClass)));
    }

    #endregion

    #region Порталы

    private void UpdatePortal(Entity<NetherPortalComponent> ent, TimeSpan now)
    {
        var portal = ent.Comp;
        portal.Spawned.RemoveWhere(m => TerminatingOrDeleted(m) || _mobState.IsDead(m));

        if (portal.Spawned.Count < portal.MaxMobs && now >= portal.NextSpawn)
        {
            portal.NextSpawn = now + portal.SpawnTime;
            var mob = Spawn(_random.Pick(portal.MobTypes), Transform(ent).Coordinates);
            portal.Spawned.Add(mob);
            _popup.PopupEntity(Loc.GetString("nether-portal-spawn", ("mob", mob), ("portal", ent.Owner)), ent, PopupType.MediumCaution);
        }

        if (now < portal.NextConsume || !_container.TryGetContainer(ent, NetherPortalComponent.ContainerId, out var container))
            return;

        portal.NextConsume = now + TimeSpan.FromSeconds(2);
        foreach (var victim in container.ContainedEntities.ToArray())
        {
            if (!HasComp<MobStateComponent>(victim))
                continue;

            _audio.PlayPvs(portal.ConsumeSound, ent);
            _damageable.TryChangeDamage(victim, portal.ConsumeDamage, ignoreResistances: true);
            if (!_mobState.IsDead(victim))
                continue;

            // Тело возвращается пустой оболочкой.
            var blank = Spawn(portal.BlankBody, Transform(ent).Coordinates);
            _metaData.SetEntityName(blank, Name(victim));
            var held = _container.EnsureContainer<ContainerSlot>(blank, NetherBlankBodyComponent.ContainerId);
            _container.Remove(victim, container);
            _container.Insert(victim, held);
            _popup.PopupEntity(Loc.GetString("nether-portal-reemerge", ("victim", victim)), ent, PopupType.LargeCaution);
        }
    }

    private void OnPortalInteractHand(Entity<NetherPortalComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var container = _container.EnsureContainer<Container>(ent, NetherPortalComponent.ContainerId);
        _popup.PopupEntity(Loc.GetString("nether-portal-pulled", ("user", args.User)), ent, PopupType.LargeCaution);
        _container.Insert(args.User, container);
    }

    private void OnPortalShutdown(Entity<NetherPortalComponent> ent, ref ComponentShutdown args)
    {
        if (_container.TryGetContainer(ent, NetherPortalComponent.ContainerId, out var container))
            _container.EmptyContainer(container, true, Transform(ent).Coordinates);

        if (ent.Comp.Source is { } source && TryComp<BluespaceTapComponent>(source, out var tap))
        {
            tap.ActivePortals.Remove(ent);
            UpdateVisuals((source, tap));
        }
    }

    private void OnBlankBodyState(Entity<NetherBlankBodyComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (_container.TryGetContainer(ent, NetherBlankBodyComponent.ContainerId, out var held))
            _container.EmptyContainer(held, true, Transform(ent).Coordinates);

        _popup.PopupEntity(Loc.GetString("nether-blank-dust", ("blank", ent.Owner)), ent);
        QueueDel(ent);
    }

    #endregion

    #region UI и вид

    private void UpdateVisuals(Entity<BluespaceTapComponent> ent)
    {
        var tap = ent.Comp;
        var portaling = tap.ActivePortals.Count > 0 || tap.Spawning > 0;
        var powered = tap.AvailablePower > 0 || tap.MiningPower > 0;

        BluespaceTapVisualState state;
        if (tap.ActivePortals.Count > 0)
            state = BluespaceTapVisualState.Cascade;
        else if (!powered)
            state = BluespaceTapVisualState.Off;
        else
        {
            state = tap.MiningPower switch
            {
                >= 15 * MW => BluespaceTapVisualState.Level5,
                >= 11 * MW => BluespaceTapVisualState.Level4,
                >= 8 * MW => BluespaceTapVisualState.Level3,
                >= 3 * MW => BluespaceTapVisualState.Level2,
                >= 50 * KW => BluespaceTapVisualState.Level1,
                _ => BluespaceTapVisualState.Level0,
            };
        }

        var screen = portaling ? BluespaceTapScreenState.Cascade
            : !powered ? BluespaceTapScreenState.None
            : tap.IsDirty ? BluespaceTapScreenState.Dirty
            : BluespaceTapScreenState.Screen;

        _appearance.SetData(ent, BluespaceTapVisuals.State, state);
        _appearance.SetData(ent, BluespaceTapVisuals.Screen, screen);

        if (portaling)
        {
            _light.SetEnabled(ent, true);
            _light.SetRadius(ent, 15);
            _light.SetEnergy(ent, 5);
            _light.SetColor(ent, Color.FromHex("#ff0000"));
        }
        else
        {
            _light.SetEnabled(ent, powered);
            _light.SetRadius(ent, 1.5f);
            _light.SetEnergy(ent, 1);
            _light.SetColor(ent, Color.FromHex("#353535"));
        }
    }

    private void UpdateUi(Entity<BluespaceTapComponent> ent)
    {
        var tap = ent.Comp;
        var state = new BluespaceTapUiState
        {
            DesiredMiningPower = tap.DesiredMiningPower,
            MiningPower = tap.MiningPower,
            Points = (float) Math.Floor(tap.Points),
            TotalPoints = (float) Math.Floor(tap.TotalPoints),
            PowerUse = tap.MiningPower + tap.StabilizerPower,
            AvailablePower = tap.AvailablePower,
            Emagged = IsEmagged(ent),
            Dirty = tap.IsDirty,
            AutoShutdown = tap.AutoShutdown,
            Stabilizers = tap.Stabilizers,
            StabilizerPower = tap.StabilizerPower,
            StabilizerPriority = tap.StabilizerPriority,
            Portaling = tap.ActivePortals.Count > 0 || tap.Spawning > 0,
        };

        for (var i = 0; i < tap.Products.Count; i++)
        {
            state.Products.Add(new BluespaceTapProductState(i, Loc.GetString(tap.Products[i].Name), GetCost(tap, i)));
        }

        _ui.SetUiState(ent.Owner, BluespaceTapUiKey.Key, state);
    }

    /// <summary>set_power: выше 1 МВт округляется вниз до мегаватта.</summary>
    private void OnSetPower(Entity<BluespaceTapComponent> ent, ref BluespaceTapSetPowerMessage args)
    {
        if (IsEmagged(ent) || !float.IsFinite(args.Power))
            return;

        var power = Math.Max(args.Power, 0);
        if (power > MW)
            power -= power % MW;

        ent.Comp.DesiredMiningPower = power;
        UpdateUi(ent);
    }

    private void OnVend(Entity<BluespaceTapComponent> ent, ref BluespaceTapVendMessage args)
    {
        Produce(ent, args.Key, purchased: true, doubleChance: true);
        UpdateUi(ent);
    }

    private void OnToggleAutoShutdown(Entity<BluespaceTapComponent> ent, ref BluespaceTapToggleAutoShutdownMessage args)
    {
        if (IsEmagged(ent))
            return;

        ent.Comp.AutoShutdown = !ent.Comp.AutoShutdown;
        UpdateUi(ent);
    }

    private void OnToggleStabilizers(Entity<BluespaceTapComponent> ent, ref BluespaceTapToggleStabilizersMessage args)
    {
        if (IsEmagged(ent))
            return;

        ent.Comp.Stabilizers = !ent.Comp.Stabilizers;
        UpdateUi(ent);
    }

    private void OnTogglePriority(Entity<BluespaceTapComponent> ent, ref BluespaceTapToggleStabilizerPriorityMessage args)
    {
        if (IsEmagged(ent))
            return;

        ent.Comp.StabilizerPriority = !ent.Comp.StabilizerPriority;
        UpdateUi(ent);
    }

    #endregion
}
