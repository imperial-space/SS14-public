using Robust.Shared.Random;
using System.Linq;
using Content.Server.Light.EntitySystems;
using Content.Server.SurveillanceCamera;
using Content.Shared.Atmos.Piping.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.TerrorSpider;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

public sealed partial class TerrorSpiderSystem
{
    [Dependency] private readonly PoweredLightSystem _lights = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameras = default!;

    public static readonly EntProtoId Red = "MobTerrorSpiderRed";
    public static readonly EntProtoId Gray = "MobTerrorSpiderGray";
    public static readonly EntProtoId Green = "MobTerrorSpiderGreen";
    public static readonly EntProtoId Black = "MobTerrorSpiderBlack";
    public static readonly EntProtoId White = "MobTerrorSpiderWhite";
    public static readonly EntProtoId Purple = "MobTerrorSpiderPurple";
    public static readonly EntProtoId Brown = "MobTerrorSpiderBrown";
    public static readonly EntProtoId Prince = "MobTerrorSpiderPrince";
    public static readonly EntProtoId Princess = "MobTerrorSpiderPrincess";
    public static readonly EntProtoId Mother = "MobTerrorSpiderMother";
    public static readonly EntProtoId Queen = "MobQueenSpider";

    private static readonly EntProtoId EggCluster = "TerrorEggCluster";
    private static readonly EntProtoId Spiderling = "MobTerrorSpiderling";
    private static readonly EntProtoId NestAction = "ActionTerrorSpiderQueenNest";
    private static readonly EntProtoId QueenEggsAction = "ActionTerrorSpiderQueenEggs";
    private static readonly EntProtoId HiveSenseAction = "ActionTerrorSpiderHiveSense";

    /// <summary>TS_DESC_*: подпись типа в окне кладки.</summary>
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        [Red] = "terror-spider-desc-red",
        [Gray] = "terror-spider-desc-gray",
        [Green] = "terror-spider-desc-green",
        [White] = "terror-spider-desc-white",
        [Black] = "terror-spider-desc-black",
        [Purple] = "terror-spider-desc-purple",
        [Brown] = "terror-spider-desc-brown",
        [Prince] = "terror-spider-desc-prince",
        [Princess] = "terror-spider-desc-princess",
        [Mother] = "terror-spider-desc-mother",
        [Queen] = "terror-spider-desc-queen",
    };

    private static readonly EntProtoId[] StandardTypes = { Red, Gray, Green, Black };
    private static readonly EntProtoId[] UncappedTypes = { Red, Gray, Green };

    private void InitializeHive()
    {
        SubscribeLocalEvent<TerrorSpiderlingComponent, Content.Shared.Mobs.MobStateChangedEvent>(OnSpiderlingDied);
    }

    #region Подсчёт

    private string? ProtoId(EntityUid uid)
    {
        return MetaData(uid).EntityPrototype?.ID;
    }

    /// <summary>CountSpiders(): все живые пауки.</summary>
    private int CountSpiders()
    {
        return LivingSpiders().Count();
    }

    /// <summary>CountSpidersDetailed(): живые пауки, яйца и паучата по типам.</summary>
    private Dictionary<string, int> CountSpidersDetailed(EntityUid self, bool checkMine, params EntProtoId[] types)
    {
        var totals = new Dictionary<string, int> { ["all"] = 0 };
        var filter = types.Length > 0 ? types.Select(t => t.Id).ToHashSet() : null;

        void Add(string? type, int amount)
        {
            if (type == null || filter != null && !filter.Contains(type))
                return;
            totals[type] = totals.GetValueOrDefault(type) + amount;
            totals["all"] += amount;
        }

        foreach (var spider in LivingSpiders())
        {
            if (spider.Owner == self || checkMine && spider.Comp.Queen != self)
                continue;
            Add(ProtoId(spider), 1);
        }

        var eggs = EntityQueryEnumerator<TerrorEggClusterComponent>();
        while (eggs.MoveNext(out _, out var egg))
        {
            if (checkMine && egg.Queen != self)
                continue;
            Add(egg.SpiderlingType?.Id, egg.SpiderlingNumber);
        }

        var lings = EntityQueryEnumerator<TerrorSpiderlingComponent>();
        while (lings.MoveNext(out var uid, out var ling))
        {
            if (ling.Stillborn || _mobState.IsDead(uid) || checkMine && ling.Queen != self)
                continue;
            Add(ling.GrowAs?.Id, 1);
        }

        return totals;
    }

    /// <summary>DoHiveSense(): список всего выводка.</summary>
    private void OnHiveSenseAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderHiveSenseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SendTo(ent, Loc.GetString("terror-spider-hive-sense-header"));
        var query = EntityQueryEnumerator<TerrorSpiderComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            string state;
            if (_mobState.IsDead(uid))
            {
                state = Loc.GetString("terror-spider-hive-sense-dead");
            }
            else
            {
                var health = 0;
                var max = 0;
                if (_thresholds.TryGetThresholdForState(uid, Content.Shared.Mobs.MobState.Dead, out var threshold)
                    && TryComp<DamageableComponent>(uid, out var damageable))
                {
                    max = threshold.Value.Int();
                    health = (threshold.Value - _damageable.GetTotalDamage((uid, damageable))).Int();
                }

                state = Loc.GetString("terror-spider-hive-sense-health", ("health", health), ("max", max));
            }

            var control = Loc.GetString(HasComp<ActorComponent>(uid) ? "terror-spider-hive-sense-player" : "terror-spider-hive-sense-ai");
            SendTo(ent, Loc.GetString("terror-spider-hive-sense-line", ("spider", uid), ("area", AreaName(uid)), ("state", state), ("control", control)));
        }
    }

    #endregion

    #region Яйца и паучата

    /// <summary>DoLayTerrorEggs(): кладка из lay_number паучат типа lay_type.</summary>
    public void DoLayTerrorEggs(Entity<TerrorSpiderComponent> ent, EntProtoId layType, int number)
    {
        var cluster = Spawn(EggCluster, _transform.GetMoverCoordinates(ent));
        var eggs = EnsureComp<TerrorEggClusterComponent>(cluster);
        eggs.SpiderlingType = layType;
        eggs.SpiderlingNumber = number;
        eggs.Queen = ent.Comp.Queen;
        eggs.Mother = ent;
        eggs.Enemies = new HashSet<EntityUid>(ent.Comp.Enemies);

        var typeName = Descriptions.TryGetValue(layType, out _) ? layType.Id : Red.Id;
        _metaData.SetEntityName(cluster, Loc.GetString("terror-eggs-name", ("type", typeName)));
    }

    private void UpdateHive(TimeSpan now)
    {
        if (now < _nextHiveTick)
            return;

        _nextHiveTick = now + LifeInterval;

        // terror_eggcluster/process()
        var eggs = EntityQueryEnumerator<TerrorEggClusterComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            egg.AmountGrown += _random.Next(0, 3);
            if (egg.AmountGrown < 100)
                continue;

            var coords = _transform.GetMoverCoordinates(uid);
            for (var i = 0; i < egg.SpiderlingNumber; i++)
            {
                var ling = Spawn(Spiderling, coords);
                var comp = EnsureComp<TerrorSpiderlingComponent>(ling);
                comp.GrowAs = egg.SpiderlingType;
                comp.Queen = egg.Queen;
                comp.Mother = egg.Mother;
                comp.Enemies = new HashSet<EntityUid>(egg.Enemies);
            }

            QueueDel(uid);
        }

        // terror_spiderling/Life(): растёт только на полу (на спине матери — нет).
        var lings = EntityQueryEnumerator<TerrorSpiderlingComponent>();
        while (lings.MoveNext(out var uid, out var ling))
        {
            if (_mobState.IsDead(uid) || _container.IsEntityInContainer(uid))
                continue;

            // Рост и в базовом spiderling/Life(), и в terror_spiderling/Life().
            ling.AmountGrown += _random.Next(0, 3) + _random.Next(0, 3);
            if (ling.AmountGrown < 100)
                continue;

            if (ling.Stillborn)
            {
                // Мертворождённые остаются подольше — для жути.
                if (ling.AmountGrown >= 300)
                    QueueDel(uid);
                continue;
            }

            var growAs = ling.GrowAs ?? _random.Pick(UncappedTypes);
            var spider = Spawn(growAs, _transform.GetMoverCoordinates(uid));
            if (TryComp<TerrorSpiderComponent>(spider, out var comp))
            {
                comp.Queen = ling.Queen;
                comp.Mother = ling.Mother;
                comp.Enemies = new HashSet<EntityUid>(ling.Enemies);
            }

            QueueDel(uid);
        }
    }

    private TimeSpan _nextHiveTick;

    /// <summary>spiderling/death(): остаются зелёные останки.</summary>
    private void OnSpiderlingDied(Entity<TerrorSpiderlingComponent> ent, ref Content.Shared.Mobs.MobStateChangedEvent args)
    {
        if (args.NewMobState != Content.Shared.Mobs.MobState.Dead)
            return;

        if (!_container.IsEntityInContainer(ent))
            Spawn("TerrorSpiderlingRemains", _transform.GetMoverCoordinates(ent));
        QueueDel(ent);
    }

    #endregion

    #region Зелёный

    /// <summary>DoLayGreenEggs().</summary>
    private void OnGreenEggsAction(Entity<TerrorSpiderGreenComponent> ent, ref TerrorSpiderGreenEggsActionEvent args)
    {
        if (args.Handled || !TryComp<TerrorSpiderComponent>(ent, out var spider))
            return;

        args.Handled = true;
        if (!CanLayGreenEggs((ent, spider, ent.Comp)))
            return;

        var types = GreenEggTypes((ent, spider));
        OpenChoice(ent, TerrorSpiderChoiceMode.Eggs, Loc.GetString("terror-spider-eggs-title"), EggOptions(types));
    }

    private bool CanLayGreenEggs(Entity<TerrorSpiderComponent, TerrorSpiderGreenComponent> ent)
    {
        if (_lookup.GetEntitiesInRange<TerrorEggClusterComponent>(_transform.GetMapCoordinates(ent), 0.4f).Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-eggs-already"), ent, ent);
            return false;
        }

        if (ent.Comp1.Fed < ent.Comp2.FeedingsToLay)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-eggs-wrap-more"), ent, ent);
            return false;
        }

        return true;
    }

    private List<EntProtoId> GreenEggTypes(Entity<TerrorSpiderComponent> ent)
    {
        var types = new List<EntProtoId> { Red, Gray, Green };
        var counts = CountSpidersDetailed(ent, false);
        if (counts.GetValueOrDefault(Brown) < 2)
            types.Add(Brown);
        if (counts.GetValueOrDefault(Black) < 2)
            types.Add(Black);
        return types;
    }

    private void LayGreenEggs(Entity<TerrorSpiderComponent, TerrorSpiderGreenComponent> ent, EntProtoId type)
    {
        if (!OnFloor(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-eggs-floor"), ent, ent);
            return;
        }

        if (!CanLayGreenEggs(ent))
            return;

        _popup.PopupEntity(Loc.GetString("terror-spider-eggs-lays", ("spider", ent.Owner)), ent);
        DoLayTerrorEggs((ent, ent.Comp1), type, 1);
        ent.Comp1.Fed -= ent.Comp2.FeedingsToLay;
    }

    #endregion

    #region Королева, принцесса, императрица

    private void OnQueenInit(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        // spider_myqueen = src
        ent.Comp1.Queen = ent;
        ent.Comp2.LastSpawn = _timing.CurTime;

        if (ent.Comp2.Princess || ent.Comp2.Empress)
        {
            // Принцессы не гнездятся: сразу могут откладывать яйца.
            ent.Comp2.HasNested = true;
            _actions.AddAction(ent, QueenEggsAction);
            _actions.AddAction(ent, HiveSenseAction);
        }
        else
        {
            _actions.AddAction(ent, NestAction);
        }

        if (ent.Comp2.Princess)
            UpdatePrincessStage(ent);
    }

    private void QueenLife(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        if (!HasComp<ActorComponent>(ent) || !ent.Comp2.HasNested || ent.Comp2.Empress)
            return;

        if (_timing.CurTime > ent.Comp2.LastSpawn + ent.Comp2.SpawnFrequency)
            GrantEggs(ent);
    }

    /// <summary>grant_eggs(): королеве +level яиц, принцессе +1 до предела выводка.</summary>
    private void GrantEggs(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        var queen = ent.Comp2;
        queen.LastSpawn = _timing.CurTime;

        if (queen.Princess)
        {
            if (!OnFloor(ent))
            {
                _popup.PopupEntity(Loc.GetString("terror-spider-princess-hiding"), ent, ent);
                return;
            }

            var brood = UpdatePrincessStage(ent);
            if (brood + queen.CanLay >= queen.MaxChildren)
                return;

            queen.CanLay++;
        }
        else
        {
            // getSpiderLevel() = 1 + минуты жизни / 10
            queen.CanLay += 1 + MinutesAlive(ent.Comp1) / 10;
        }

        _popup.PopupEntity(queen.CanLay == 1
            ? Loc.GetString("terror-spider-eggs-available-one")
            : Loc.GetString("terror-spider-eggs-available", ("count", queen.CanLay)), ent, ent);
    }

    /// <summary>Окрас принцессы меняется от занятости выводка.</summary>
    private int UpdatePrincessStage(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        var brood = CountSpidersDetailed(ent, true)["all"];
        var stage = brood == 0 ? 0 : brood < ent.Comp2.MaxChildren / 2 ? 1 : 2;
        _appearance.SetData(ent, TerrorSpiderVisuals.Stage, stage);
        _metaData.SetEntityDescription(ent, Loc.GetString($"terror-spider-princess-desc-{stage + 1}"));
        return brood;
    }

    private void OnQueenNestAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderQueenNestActionEvent args)
    {
        if (args.Handled || ent.Comp.HasNested)
            return;

        args.Handled = true;
        OpenChoice(ent, TerrorSpiderChoiceMode.Nest, Loc.GetString("terror-spider-nest-prompt"), new[]
        {
            new TerrorSpiderChoiceOption("yes", Loc.GetString("terror-spider-nest-yes")),
            new TerrorSpiderChoiceOption("no", Loc.GetString("terror-spider-nest-no")),
        });
    }

    /// <summary>NestMode(): яйца и стены, но без вентиляции.</summary>
    public void NestMode(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        if (ent.Comp2.Princess)
            return;

        ent.Comp2.HasNested = true;
        _actions.AddAction(ent, QueenEggsAction);
        _actions.AddAction(ent, HiveSenseAction);
        foreach (var action in _actions.GetActions(ent))
        {
            if (ProtoId(action.Owner) == NestAction.Id)
                _actions.RemoveAction(ent.Owner, action.Owner);
        }

        RemComp<VentCrawlerComponent>(ent);
        ent.Comp1.AiVentcrawls = false;
        ent.Comp1.SmashWalls = true;
        DoQueenScreech(ent, 8, 100, 8, 100);
        _popup.PopupEntity(Loc.GetString("terror-spider-nest-matured"), ent, ent, PopupType.Medium);
    }

    /// <summary>DoQueenScreech(): бьёт лампы и выключает камеры вокруг.</summary>
    private void DoQueenScreech(EntityUid queen, float lightRange, int lightChance, float cameraRange, int cameraChance)
    {
        _popup.PopupEntity(Loc.GetString("terror-spider-queen-screech", ("spider", queen)), queen, PopupType.LargeCaution);
        var coords = _transform.GetMapCoordinates(queen);
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(coords, lightRange))
        {
            if (light.Comp.On && _random.Prob(lightChance / 100f))
                _lights.TryDestroyBulb(light, light.Comp);
        }

        foreach (var camera in _lookup.GetEntitiesInRange<SurveillanceCameraComponent>(coords, cameraRange))
        {
            if (camera.Comp.Active && _random.Prob(cameraChance / 100f))
                _cameras.SetActive(camera, false, camera.Comp);
        }
    }

    /// <summary>LayQueenEggs().</summary>
    private void OnQueenEggsAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderQueenEggsActionEvent args)
    {
        if (args.Handled || !TryComp<TerrorSpiderComponent>(ent, out var spider))
            return;

        args.Handled = true;
        if (!ent.Comp.HasNested)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-nest-first"), ent, ent);
            return;
        }

        if (ent.Comp.CanLay < 1)
        {
            ShowEggTimer((ent, spider, ent.Comp));
            return;
        }

        var types = ListAvailableEggTypes((ent, spider, ent.Comp));
        int[] counts;
        if (ent.Comp.Empress)
            counts = new[] { 1, 2, 3, 4, 5, 10, 15, 20, 30, 40, 50 };
        else if (ent.Comp.CanLay >= 5)
            counts = new[] { 1, 2, 3, 4, 5 };
        else if (ent.Comp.CanLay >= 3)
            counts = new[] { 1, 2, 3 };
        else if (ent.Comp.CanLay == 2)
            counts = new[] { 1, 2 };
        else
            counts = new[] { 1 };

        OpenChoice(ent, TerrorSpiderChoiceMode.Eggs, Loc.GetString("terror-spider-eggs-title"), EggOptions(types), counts);
    }

    private TerrorSpiderChoiceOption[] EggOptions(IEnumerable<EntProtoId> types)
    {
        return types.Select(t => new TerrorSpiderChoiceOption(t.Id, Loc.GetString(Descriptions[t]))).ToArray();
    }

    /// <summary>show_egg_timer().</summary>
    private void ShowEggTimer(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        if (ent.Comp2.Princess)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-eggs-timer-princess", ("seconds", (int) ent.Comp2.SpawnFrequency.TotalSeconds)), ent, ent);
            return;
        }

        var remaining = (int) Math.Round((ent.Comp2.LastSpawn + ent.Comp2.SpawnFrequency - _timing.CurTime).TotalSeconds);
        _popup.PopupEntity(remaining > 0
            ? Loc.GetString("terror-spider-eggs-timer", ("seconds", remaining))
            : Loc.GetString("terror-spider-eggs-timer-soon"), ent, ent);
    }

    /// <summary>ListAvailableEggTypes().</summary>
    private List<EntProtoId> ListAvailableEggTypes(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        if (ent.Comp2.Empress)
            return new List<EntProtoId> { Queen, Mother, Prince, Princess, Red, Gray, Green, Black, Purple, White, Brown };

        if (ent.Comp2.Princess)
        {
            var types = new List<EntProtoId> { Red, Gray, Green };
            // У принцессы может быть ОДИН чёрный/фиолетовый/коричневый.
            if (CountSpidersDetailed(ent, true, Black, Purple, Brown)["all"] < 1)
                types.AddRange(new[] { Black, Purple, Brown });
            return types;
        }

        if (MinutesAlive(ent.Comp1) >= 20 && CountSpidersDetailed(ent, true, Mother, Prince, Princess)["all"] == 0)
            return new List<EntProtoId> { Prince, Princess, Mother };

        var valid = new List<EntProtoId> { Red, Gray, Green };
        var counts = CountSpidersDetailed(ent, false, Brown, Purple, Black);
        if (counts.GetValueOrDefault(Brown) < 2)
            valid.Add(Brown);
        if (counts.GetValueOrDefault(Purple) < 2)
            valid.Add(Purple);
        if (counts.GetValueOrDefault(Black) < 2)
            valid.Add(Black);
        return valid;
    }

    private void OnEggChoice(Entity<TerrorSpiderComponent> ent, string key, int count)
    {
        var type = new EntProtoId(key);
        if (TryComp<TerrorSpiderGreenComponent>(ent, out var green))
        {
            if (GreenEggTypes(ent).Contains(type))
                LayGreenEggs((ent, ent.Comp, green), type);
            return;
        }

        if (!TryComp<TerrorSpiderQueenComponent>(ent, out var queen))
            return;

        var queenEnt = (ent.Owner, ent.Comp, queen);
        if (!ListAvailableEggTypes(queenEnt).Contains(type))
            return;

        var number = Math.Max(1, count);
        if (!UncappedTypes.Contains(type) && !queen.Empress)
            number = 1;

        if (!OnFloor(ent))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-eggs-floor"), ent, ent);
            return;
        }

        if (!queen.Empress && queen.CanLay < number)
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-eggs-too-soon"), ent, ent);
            return;
        }

        if (!queen.Empress)
            queen.CanLay -= number;
        queen.EggsLaid += number;
        DoLayTerrorEggs(ent, type, number);
        if (queen.Princess)
            UpdatePrincessStage(queenEnt);
    }

    /// <summary>EmpressLings().</summary>
    private void OnEmpressLingsAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderEmpressLingsActionEvent args)
    {
        if (args.Handled || !ent.Comp.Empress)
            return;

        args.Handled = true;
        OpenChoice(ent, TerrorSpiderChoiceMode.Spiderlings, Loc.GetString("terror-spider-empress-lings-title"),
            new[] { new TerrorSpiderChoiceOption("lings", Loc.GetString("terror-spider-empress-lings-option")) },
            new[] { 10, 20, 30, 40, 50 }, new[] { 0, 25, 50, 75, 100 });
    }

    private void EmpressLings(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent, int count, int stillbornChance)
    {
        var coords = _transform.GetMoverCoordinates(ent);
        for (var i = 0; i < Math.Clamp(count, 0, 50); i++)
        {
            var ling = Spawn(Spiderling, coords);
            var comp = EnsureComp<TerrorSpiderlingComponent>(ling);
            comp.GrowAs = _random.Pick(new[] { Red, Gray, Green, White, Black });
            comp.Queen = ent.Comp1.Queen;
            comp.Mother = ent;
            comp.Stillborn = _random.Prob(Math.Clamp(stillbornChance, 0, 100) / 100f);
        }
    }

    /// <summary>EraseBrood(): все пауки младше императрицы вырождаются, яйца и паучата исчезают.</summary>
    private void OnEmpressEraseAction(Entity<TerrorSpiderQueenComponent> ent, ref TerrorSpiderEmpressEraseActionEvent args)
    {
        if (args.Handled || !ent.Comp.Empress || !TryComp<TerrorSpiderComponent>(ent, out var self))
            return;

        args.Handled = true;
        foreach (var spider in LivingSpiders())
        {
            if (spider.Comp.Tier >= self.Tier)
                continue;
            spider.Comp.Degenerate = true;
            SendTo(spider, Loc.GetString("terror-spider-empress-erase-victim", ("empress", ent.Owner)));
        }

        var eggs = EntityQueryEnumerator<TerrorEggClusterComponent>();
        while (eggs.MoveNext(out var uid, out _))
        {
            QueueDel(uid);
        }

        var lings = EntityQueryEnumerator<TerrorSpiderlingComponent>();
        while (lings.MoveNext(out var uid, out _))
        {
            QueueDel(uid);
        }

        SendTo(ent, Loc.GetString("terror-spider-empress-erase"));
    }

    #endregion

    #region Смерть и фиолетовые

    private void OnSpiderDeath(Entity<TerrorSpiderComponent> ent)
    {
        if (HasComp<TerrorSpiderMotherComponent>(ent))
            DropSpiderlings(ent);

        if (TryComp<TerrorSpiderPurpleComponent>(ent, out var purple))
            PurpleDeath((ent, ent.Comp, purple));

        if (HasComp<TerrorSpiderQueenComponent>(ent))
            QueenDeath(ent);
    }

    /// <summary>
    /// queen/death(): умирают все её паучата и половина взрослого потомства —
    /// так экипаж может победить даже огромный выводок, сняв королеву.
    /// </summary>
    private void QueenDeath(Entity<TerrorSpiderComponent> ent)
    {
        foreach (var spider in LivingSpiders())
        {
            if (spider.Owner == ent.Owner || spider.Comp.Queen != ent.Owner)
                continue;

            if (_random.Prob(0.5f) || spider.Comp.Tier >= ent.Comp.Tier)
            {
                SendTo(spider, Loc.GetString("terror-spider-queen-died-survive", ("queen", ent.Owner)));
                continue;
            }

            _popup.PopupEntity(Loc.GetString("terror-spider-writhes", ("spider", spider.Owner)), spider, PopupType.MediumCaution);
            SendTo(spider, Loc.GetString("terror-spider-queen-died-degenerate", ("queen", ent.Owner)));
            spider.Comp.Degenerate = true;
        }

        var lings = EntityQueryEnumerator<TerrorSpiderlingComponent>();
        while (lings.MoveNext(out var uid, out var ling))
        {
            if (ling.Queen == ent.Owner)
                QueueDel(uid);
        }
    }

    /// <summary>purple/death(): погибший вдали страж заменяется новым яйцом королевы.</summary>
    private void PurpleDeath(Entity<TerrorSpiderComponent, TerrorSpiderPurpleComponent> ent)
    {
        if (ent.Comp1.Queen is not { } queen || !TryComp<TerrorSpiderComponent>(queen, out var queenSpider)
            || _mobState.IsDead(queen) || HasComp<ActorComponent>(queen))
            return;

        if (InRange(ent, queen, ent.Comp2.MaxQueenRange) || ent.Comp1.Degenerate || queenSpider.Degenerate)
            return;

        ent.Comp1.Degenerate = true;
        DoLayTerrorEggs((queen, queenSpider), Purple, 1);
        _popup.PopupEntity(Loc.GetString("terror-spider-purple-chitters", ("spider", ent.Owner), ("queen", queen)), ent);
    }

    /// <summary>purple_distance_check() раз в 5 тиков Life.</summary>
    private void PurpleLife(Entity<TerrorSpiderComponent, TerrorSpiderPurpleComponent> ent)
    {
        var purple = ent.Comp2;
        purple.LifeTicks++;
        if (ent.Comp1.Degenerate || ent.Comp1.Queen is not { } queen || purple.LifeTicks % 5 != 0)
            return;

        if (!Exists(queen) || _mobState.IsDead(queen))
        {
            ent.Comp1.Queen = null;
            ent.Comp1.Degenerate = true;
            SendTo(ent, Loc.GetString("terror-spider-purple-queen-died", ("queen", queen)));
            return;
        }

        // vision_range = 10
        purple.QueenVisible = InRange(ent, queen, 10);
        purple.CyclesNoQueen = purple.QueenVisible ? 0 : purple.CyclesNoQueen + 1;
        var area = AreaName(queen);
        switch (purple.CyclesNoQueen)
        {
            case 6:
                SendTo(ent, Loc.GetString("terror-spider-purple-separated", ("queen", queen), ("area", area)));
                break;
            case 12:
                SendTo(ent, Loc.GetString("terror-spider-purple-weakens", ("queen", queen), ("area", area)));
                break;
            case 18:
                ent.Comp1.Degenerate = true;
                SendTo(ent, Loc.GetString("terror-spider-purple-broken", ("queen", queen)));
                ent.Comp1.MeleeLower = 5;
                ent.Comp1.MeleeUpper = 10;
                break;
        }
    }

    #endregion
}
