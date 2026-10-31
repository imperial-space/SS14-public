using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Imperial.ZLevels;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Maps;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Управление ивентом «Оборона Кадии».
///   cadia start                       — запустить правило войны
///   cadia status                      — фаза, таймеры, пилоны, цитадель
///   cadia phase next|&lt;фаза&gt;           — перейти к следующей / указанной фазе
///   cadia wave imperium|chaos         — немедленная волна подкреплений
///   cadia win imperium|chaos          — объявить победителя
///   cadia testmap                     — пустое поле боя для проверки: пол, воздух, гравитация, пилоны, цитадель, точки высадки
///   cadia map                         — боевая карта на 100 игроков по черновику battlefield.txt, с небом и орбитой
///   cadia panel                       — окно ведущего: фазы, волны, штормы, объявления, игроки на поле
///   cadia whitelist add|remove|list [ckey] — кому доступны командные роли (хранится между перезапусками)
///   cadia lobby on|off                — оформление лобби ивента: фон и музыка
///   cadia weather clear|ash|fog|smog  — сменить погоду
///   cadia ending &lt;концовка&gt;            — посмотреть финальную сцену самому, война не заканчивается
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class CadiaCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _cadia = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;
    [Dependency] private readonly TileSystem _tileSystem = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly Content.Shared.Maps.TurfSystem _turf = default!;
    [Dependency] private readonly CadiaEventPanelSystem _panel = default!;
    [Dependency] private readonly CadiaFrontlineSystem _frontline = default!;
    [Dependency] private readonly CadiaWeatherSystem _weather = default!;
    [Dependency] private readonly Robust.Shared.ContentPack.IResourceManager _res = default!;
    [Dependency] private readonly Content.Server.Decals.DecalSystem _decals = default!;
    [Dependency] private readonly Content.Shared.Weather.SharedWeatherSystem _snow = default!;
    [Dependency] private readonly Content.Shared.Light.EntitySystems.SharedRoofSystem _roof = default!;

    private static readonly Color BattlefieldDusk = Color.FromHex("#7e7686");
    private static readonly Robust.Shared.Prototypes.EntProtoId SnowFar = "CadiaWeatherSnowFar";
    private static readonly Robust.Shared.Prototypes.EntProtoId SnowNear = "CadiaWeatherSnowNear";

    /// <summary>Снег в два слоя: дальний ставится вместо любой погоды карты, ближний добавляется поверх.</summary>
    private void LetItSnow(MapId map)
    {
        _snow.TrySetWeather(map, SnowFar, out _);
        _snow.TryAddWeather(map, SnowNear, out _);
    }
    [Dependency] private readonly Robust.Shared.Prototypes.IPrototypeManager _proto = default!;
    [Dependency] private readonly Robust.Shared.Configuration.IConfigurationManager _cfg = default!;

    private const string LobbyMusic = "CadiaLobbyMusic";
    private static readonly List<Robust.Shared.Prototypes.ProtoId<Content.Shared.GameTicking.Prototypes.LobbyBackgroundPrototype>> LobbyBackgrounds =
        new() { "CadiaLobby1", "CadiaLobby2", "CadiaLobby3" };

    public const string RuleId = "CadiaDefense";

    public override string Command => "cadia";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(new[] { "start", "status", "phase", "wave", "win", "testmap", "map", "panel", "whitelist", "lobby", "weather", "ending" }),
            2 when args[0] == "weather" => CompletionResult.FromOptions(Enum.GetNames<CadiaWeather>()),
            2 when args[0] == "ending" => CompletionResult.FromOptions(Enum.GetNames<CadiaEnding>()),
            2 when args[0] == "whitelist" => CompletionResult.FromOptions(new[] { "add", "remove", "list" }),
            2 when args[0] == "lobby" => CompletionResult.FromOptions(new[] { "on", "off" }),
            3 when args[0] == "whitelist" => CompletionResult.FromHint(Loc.GetString("cmd-cadia-whitelist-hint")),
            2 when args[0] == "phase" => CompletionResult.FromOptions(new[] { "next" }.Concat(Enum.GetNames<CadiaWarPhase>())),
            2 when args[0] is "wave" or "win" => CompletionResult.FromOptions(new[] { "imperium", "chaos" }),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (args[0] == "start")
        {
            if (FindRule() != null)
            {
                shell.WriteError(Loc.GetString("cmd-cadia-already"));
                return;
            }
            _ticker.StartGameRule(RuleId);
            shell.WriteLine(Loc.GetString("cmd-cadia-started"));
            return;
        }

        if (args[0] == "whitelist")
        {
            Whitelist(shell, args);
            return;
        }

        if (args[0] == "ending")
        {
            if (args.Length < 2 || !Enum.TryParse<CadiaEnding>(args[1], true, out var ending) || shell.Player is not { } viewer)
            {
                shell.WriteError(Help);
                return;
            }
            EntityManager.EntityNetManager.SendSystemNetworkMessage(new CadiaEndingEvent(ending, 30), viewer.Channel);
            return;
        }

        if (args[0] == "weather")
        {
            if (args.Length < 2 || !Enum.TryParse<CadiaWeather>(args[1], true, out var weather))
            {
                shell.WriteError(Help);
                return;
            }
            _weather.Set(weather);
            return;
        }

        if (args[0] == "lobby")
        {
            var on = args.Length >= 2 && args[1] == "on";
            _ticker.SetEventLobbyBackgrounds(on ? LobbyBackgrounds : null);
            _cfg.SetCVar(Content.Shared.CCVar.CCVars.LobbyMusicCollection, on ? LobbyMusic : Content.Shared.CCVar.CCVars.LobbyMusicCollection.DefaultValue);
            shell.WriteLine(Loc.GetString(on ? "cmd-cadia-lobby-on" : "cmd-cadia-lobby-off"));
            return;
        }

        if (args[0] == "panel")
        {
            if (shell.Player is { } admin)
                _panel.Open(admin);
            return;
        }

        if (args[0] == "zcheck")
        {
            ZCheck(shell);
            return;
        }

        if (args[0] == "testmap")
        {
            TestMap(shell);
            return;
        }

        if (args[0] == "map")
        {
            TestMap(shell, true);
            return;
        }

        if (FindRule() is not { } rule || !_cadia.TryGetWar(rule.Comp, out var war))
        {
            shell.WriteError(Loc.GetString("cmd-cadia-no-rule"));
            return;
        }

        switch (args[0])
        {
            case "status":
                var now = IoCManager.Resolve<Robust.Shared.Timing.IGameTiming>().CurTime;
                shell.WriteLine(Loc.GetString("cmd-cadia-status",
                    ("phase", war.Phase.ToString()),
                    ("left", (int) Math.Max(0, (war.PhaseEnd - now).TotalSeconds)),
                    ("alive", war.PylonsAlive), ("total", war.PylonsTotal),
                    ("citadel", (int) (war.CitadelProgress * 100)),
                    ("warp", war.WarpPower),
                    ("imperium", (int) Math.Max(0, (war.NextImperiumWave - now).TotalSeconds)),
                    ("chaos", (int) Math.Max(0, (war.NextChaosWave - now).TotalSeconds))));
                break;
            case "phase" when args.Length >= 2:
                CadiaWarPhase phase;
                if (args[1] == "next")
                    phase = war.Phase >= CadiaWarPhase.Finale ? CadiaWarPhase.Finale : war.Phase + 1;
                else if (!Enum.TryParse(args[1], true, out phase) || phase == CadiaWarPhase.Ended)
                {
                    shell.WriteError(Help);
                    return;
                }
                _cadia.SetPhase(rule, phase);
                break;
            case "wave" when args.Length >= 2 && ParseFaction(args[1]) is { } waveFaction:
                _cadia.Wave(rule, war, waveFaction);
                break;
            case "win" when args.Length >= 2 && ParseFaction(args[1]) is { } winner:
                _cadia.Victory(rule, winner, Loc.GetString("cadia-win-admin"));
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }

    private void Whitelist(IConsoleShell shell, string[] args)
    {
        if (args.Length >= 2 && args[1] == "list")
        {
            shell.WriteLine(Loc.GetString("cmd-cadia-whitelist-list", ("count", _frontline.Whitelist.Count), ("names", string.Join(", ", _frontline.Whitelist))));
            return;
        }
        if (args.Length < 3 || args[1] is not ("add" or "remove"))
        {
            shell.WriteError(Help);
            return;
        }
        var ckey = args[2];
        if (args[1] == "add")
            shell.WriteLine(Loc.GetString(_frontline.AddToWhitelist(ckey) ? "cmd-cadia-whitelist-added" : "cmd-cadia-whitelist-already", ("name", ckey)));
        else
            shell.WriteLine(Loc.GetString(_frontline.RemoveFromWhitelist(ckey) ? "cmd-cadia-whitelist-removed" : "cmd-cadia-whitelist-missing", ("name", ckey)));
    }

    private static CadiaFaction? ParseFaction(string arg) => arg switch
    {
        "imperium" => CadiaFaction.Imperium,
        "chaos" => CadiaFaction.Chaos,
        _ => null,
    };

    private Entity<CadiaDefenseRuleComponent>? FindRule()
    {
        var query = EntityManager.EntityQueryEnumerator<CadiaDefenseRuleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ticker.IsGameRuleActive(uid))
                return (uid, comp);
        }
        return null;
    }

    /// <summary>
    /// Поле боя 120×80 из асфальта прямо на карте (как планета): воздух, гравитация, пасмурный кадийский день.
    /// Слева — цитадель и позиции Империума, по полю — пять пилонов, справа — плацдарм Хаоса.
    /// </summary>
    private void TestMap(IConsoleShell shell, bool battlefield = false)
    {
        _builtShips.Clear();
        var mapUid = _map.CreateMap(out var mapId, false);
        _meta.SetEntityName(mapUid, battlefield ? "Cadia — battlefield" : "Cadia — test field");

        var grid = EntityManager.EnsureComponent<MapGridComponent>(mapUid);
        var gravity = EntityManager.EnsureComponent<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        EntityManager.Dirty(mapUid, gravity);

        var light = EntityManager.EnsureComponent<MapLightComponent>(mapUid);
        // Боевая карта — холодные сумерки под Оком Ужаса: огонь, прожекторы и пилоны горят пятнами света.
        light.AmbientLightColor = battlefield ? BattlefieldDusk : Color.FromHex("#b7ad9c");
        EntityManager.Dirty(mapUid, light);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 21.824779f;
        moles[(int) Gas.Nitrogen] = 82.10312f;
        _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

        var random = new System.Random(mapId.GetHashCode());
        Tile Ground(string id) => _tileSystem.GetVariantTile((ContentTileDefinition) _tiles[id], random);
        void Place(string proto, float x, float y) =>
            EntityManager.SpawnEntity(proto, new EntityCoordinates(mapUid, new Vector2(x + 0.5f, y + 0.5f)));
        var arrival = battlefield
            ? BuildBattlefield(shell, BattlefieldLayout, mapUid, mapId, grid, Ground, Place, random)
            : TestField(shell, mapUid, mapId, grid, Ground, Place, random);

        // Над полем — небо и орбита: этажи одной стопки и точки назначения в карте консоли шаттла.
        var air = _map.CreateMap(out var airId, false);
        _meta.SetEntityName(air, Loc.GetString("cadia-flight-map-air", ("field", mapId)));
        var airLight = EntityManager.EnsureComponent<MapLightComponent>(air);
        airLight.AmbientLightColor = Color.FromHex("#c9c2b4");
        EntityManager.Dirty(air, airLight);
        _atmos.SetMapAtmosphere(air, false, new GasMixture(moles, Atmospherics.T20C));

        var orbit = _map.CreateMap(out var orbitId, false);
        _meta.SetEntityName(orbit, Loc.GetString("cadia-flight-map-orbit", ("field", mapId)));
        EntityManager.EnsureComponent<ParallaxComponent>(orbit);

        _meta.SetEntityName(mapUid, Loc.GetString("cadia-flight-map-surface", ("field", mapId)));
        var levels = new[] { (mapUid, CadiaFlightLevel.Surface), (air, CadiaFlightLevel.Air), (orbit, CadiaFlightLevel.Orbit) };
        var stack = $"cadia-{mapId}";
        for (var i = 0; i < levels.Length; i++)
        {
            var (uid, level) = levels[i];
            EntityManager.EnsureComponent<FTLDestinationComponent>(uid);
            var flightLevel = EntityManager.EnsureComponent<CadiaFlightLevelComponent>(uid);
            flightLevel.Level = level;
            flightLevel.Field = stack;
            // Небо — седьмой этаж над поверхностью (снизу его не видно, сверху Кадия далеко внизу), орбита — сразу над ним.
            _z.SetLevel(uid, stack, level switch { CadiaFlightLevel.Surface => 0, CadiaFlightLevel.Air => 7, _ => 8 });
        }
        // Орбита — открытый космос: гравитацию верхних этажей стопка включает сама, здесь её гасим.
        if (EntityManager.TryGetComponent<GravityComponent>(orbit, out var orbitGravity))
        {
            orbitGravity.Enabled = false;
            EntityManager.Dirty(orbit, orbitGravity);
        }

        // Пост флота Империума — на земле: бункер командования под цитаделью Касра (батареи, погреб, точки флота)
        // и пустотный щит во дворе цитадели. Их ставит планировка поля; крейсер держится над полем и на карте не строится.

        _map.InitializeMap(mapId);
        // Над пустошью всё время идёт снег; под крышами зданий его нет.
        if (battlefield)
            LetItSnow(mapId);
        _map.InitializeMap(airId);

        // Учебная мишень в небе над зенитками Империума: пустой шаттл, по нему можно навестись и стрелять.
        if (_loader.TryLoadGrid(airId, new ResPath("/Maps/Shuttles/mining.yml"), out var target, offset: new Vector2(-30f, 2f)))
        {
            _meta.SetEntityName(target.Value, Loc.GetString("cadia-aa-training-target"));
            EntityManager.EnsureComponent<CadiaHoverComponent>(target.Value);
        }
        if (battlefield)
        {
            ChaosFleet(shell, orbitId, orbit, Ground, random);
            ImperialFighters(shell, orbitId, orbit, Ground, random);
        }
        _map.InitializeMap(orbitId);

        // Этажи Касра: стены и крыши над полем, катакомбы и коллекторы под ним.
        var floors = new List<EntityUid>();
        if (battlefield)
        {
            floors.Add(BuildFloor(shell, stack, 1, BattlefieldUpper, moles, BattlefieldDusk, Ground, random));
            floors.Add(BuildFloor(shell, stack, -1, BattlefieldLower, moles, Color.FromHex("#141216"), Ground, random));
        }

        // Связи этажей — ещё раз после инициализации карт, и отчёт в консоль, чтобы было видно, если что-то сломало стопку.
        _z.RelinkStack(stack);
        foreach (var floor in floors)
            EntityManager.EnsureComponent<ZLevelMapComponent>(floor).NoCollapse = true;
        // На поле боя ничего не обрушается без опоры: небо и орбита пустые, корабли висят над землёй.
        foreach (var (uid, _) in levels)
            EntityManager.EnsureComponent<ZLevelMapComponent>(uid).NoCollapse = true;
        foreach (var (uid, level) in levels)
        {
            var z = EntityManager.GetComponentOrNull<ZLevelMapComponent>(uid);
            shell.WriteLine($"  {level}: Z{z?.Level} ниже={(z?.Below is { } b ? EntityManager.GetComponent<MetaDataComponent>(b).EntityName : "—")} выше={(z?.Above is { } a ? EntityManager.GetComponent<MetaDataComponent>(a).EntityName : "—")} разрыв={z?.BelowGap}");
        }

        // Воздух на палубах построенных кораблей.
        foreach (var builtShip in _builtShips)
            FillAir(builtShip);
        _cadia.RecountPylons();

        if (shell.Player?.AttachedEntity is { } player)
        {
            _xform.SetCoordinates(player, new EntityCoordinates(mapUid, arrival));
            _xform.AttachToGridOrMap(player);
        }
        shell.WriteLine(Loc.GetString(battlefield ? "cmd-cadia-map" : "cmd-cadia-testmap", ("map", mapId)));
    }

    /// <summary>Учебное поле 120×80 для проверки механик: всё вплотную, чтобы не бегать.</summary>
    private Vector2 TestField(IConsoleShell shell, EntityUid mapUid, MapId mapId, MapGridComponent grid, Func<string, Tile> ground,
        Action<string, float, float> place, System.Random random)
    {
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = -60; x < 60; x++)
        {
            for (var y = -40; y < 40; y++)
            {
                var edge = x + (int) (Math.Sin(y * 0.35) * 3);
                var id = Math.Abs(y) <= 1 && x > -36 && x < 40 ? "CadiaFloorRoad"
                    : x >= -46 && x <= -34 && Math.Abs(y) <= 6 ? "CadiaFloorFlagstone"
                    : edge < -14 ? "CadiaFloorTundra"
                    : edge < 34 ? "CadiaFloorMud"
                    : "CadiaFloorAsh";
                tiles.Add((new Vector2i(x, y), ground(id)));
            }
        }
        _map.SetTiles(mapUid, grid, tiles);


        place("CadiaCitadel", -40, 0);
        var pylons = new Vector2[] { new(-20, 22), new(-10, -18), new(0, 4), new(14, 24), new(18, -22) };
        var burnt = new List<(Vector2i, Tile)>();
        for (var i = 0; i < pylons.Length; i++)
        {
            var pylon = EntityManager.SpawnEntity("CadiaPylon", new EntityCoordinates(mapUid, pylons[i] + new Vector2(0.5f)));
            EntityManager.GetComponent<CadiaPylonComponent>(pylon).Number = i + 1;
            // Вокруг пилонов земля выжжена обстрелами.
            for (var dx = -4; dx <= 4; dx++)
            {
                for (var dy = -4; dy <= 4; dy++)
                {
                    if (dx * dx + dy * dy <= 14 + random.Next(4))
                        burnt.Add((new Vector2i((int) pylons[i].X + dx, (int) pylons[i].Y + dy), ground("CadiaFloorScorched")));
                }
            }
        }
        _map.SetTiles(mapUid, grid, burnt);

        foreach (var y in new[] { -12, -4, 4, 12 })
            place("CadiaSpawnPointImperium", -52, y);
        foreach (var y in new[] { -30, -15, 0, 15, 30 })
            place("CadiaSpawnPointChaos", 54, y);

        // Посадочная площадка Империума с шаттлом: плиты из рокрита с полосатой каймой.
        var padTiles = new List<(Vector2i, Tile)>();
        for (var x = -48; x < -32; x++)
        {
            for (var y = 18; y < 34; y++)
                padTiles.Add((new Vector2i(x, y), ground(x == -48 || x == -33 || y == 18 || y == 33 ? "CadiaDeckHazard" : "CadiaFloorRockcrete")));
        }
        _map.SetTiles(mapUid, grid, padTiles);
        BuildShip(shell, mapId, mapUid, ValkyrieLayout, new Vector2(-40f, 26f), Loc.GetString("cadia-valkyrie-name"), ground, random, CadiaFaction.Imperium);

        // Запас у площадки: парашюты, бомбы и ящик снарядов для пушек.
        for (var i = 0; i < 8; i++)
            place("CadiaParachutePack", -47 + i, 19);
        for (var i = 0; i < 4; i++)
            place("CadiaShipBomb", -47 + i, 20);
        place("CadiaFlakAmmoBox", -42, 20);

        // ПВО у позиций Империума: две «Гидры», «Мантикора» и ауспекс; трофейная «Гидра» на плацдарме Хаоса.
        place("CadiaHydraFlak", -36, 8);
        place("CadiaHydraFlak", -36, -10);
        place("CadiaManticoreLauncher", -46, -20);
        place("CadiaAuspexRadar", -42, 6);
        place("CadiaFlakAmmoBox", -38, 6);
        place("CadiaFlakAmmoBox", -38, -12);
        place("CadiaHydraFlakChaos", 46, 8);

        // Столы командования: у цитадели Империума и на плацдарме Хаоса.
        place("CadiaHoloTable", -44, -4);
        place("CadiaHoloTableChaos", 50, -6);

        // Снабжение: депо, ящики боеприпасов и зарядные станции у точек высадки обеих сторон.
        place("CadiaDepot", -48, 8);
        place("CadiaDepotChaos", 50, 10);
        place("CadiaAmmoCrateImperium", -50, 2);
        place("CadiaAmmoCrateImperium", -50, -2);
        place("CadiaPackCharger", -49, 0);
        place("CadiaAmmoCrateChaos", 52, 2);
        place("CadiaPackCharger", 51, 0);

        // Рубеж обороны: ДОТ, ПТО, гнездо, проволока, ежи, мины, склады и полевой лазарет.
        place("CadiaPillbox", -22, 18);
        place("CadiaAtGun", -18, 6);
        place("CadiaBolterNest", -18, 2);
        place("CadiaMortar", -26, 6);
        for (var y = -10; y <= 10; y += 2)
            place("CadiaSandbags", -16, y);
        for (var y = -12; y <= 12; y++)
            place("CadiaRazorWire", -12, y);
        for (var y = -12; y <= 12; y += 2)
            place("CadiaTankTrap", -9, y);
        foreach (var y in new[] { -9, -3, 3, 9 })
            place("CadiaAtMine", -6, y);
        place("CadiaStakes", -14, 14);
        place("CadiaFuelBarrel", -20, -6);
        place("CadiaAmmoCrate", -24, 2);
        place("CadiaShellCrate", -24, 10);
        place("CadiaFieldStove", -26, 0);
        place("CadiaMedicaeStation", -26, 2);
        place("CadiaStretcher", -27, 2);
        place("CadiaWireCoil", -24, 0);

        // Окоп перед рубежом и полевой госпиталь.
        for (var y = -6; y <= 6; y++)
            place("CadiaTrench", -15, y);
        place("CadiaEntrenchingTool", -17, -8);
        place("CadiaFieldStretcher", -27, 4);
        place("CadiaTent", -29, 2);

        // Укрепления Хаоса на плацдарме.
        place("CadiaChaosAltar", 48, 12);
        for (var y = -6; y <= 6; y += 2)
            place("CadiaSpikeBarricade", 40, y);
        place("CadiaWarpTrap", 36, 0);

        // Подбитая техника на нейтральной полосе: укрытия и проверка обломков.
        place("CadiaWreckRuss", 8, -14);
        place("CadiaWreckChimera", 12, 14);
        place("CadiaWreckRhino", 22, -4);
        place("CadiaWreckFire", 8, -14);

        // Секторы фронта: два у Империума, ничейный посередине, два у Хаоса.
        place("CadiaSectorA", -24, -26);
        place("CadiaSectorB", -24, 28);
        place("CadiaSectorC", 2, -8);
        place("CadiaSectorD", 26, 12);
        place("CadiaSectorE", 30, -28);

        // Маяк эвакуации на случай падения Кадии: за позициями Империума.
        place("CadiaEvacBeacon", -48, -30);

        // Разлом на плацдарме Хаоса: здесь призывают демонов.
        place("CadiaWarpRift", 52, 6);

        // Техника Империума: танк, БТР и шагоход на рубеже обороны.
        place("CadiaLemanRuss", -28, -2);
        place("CadiaChimera", -28, -8);
        place("CadiaSentinel", -30, 4);
        place("CadiaBasilisk", -44, 12);

        // Техника Хаоса на плацдарме.
        place("CadiaChaosRhino", 44, -2);
        place("CadiaHelbrute", 44, 4);
        place("CadiaDefiler", 42, -10);

        return new Vector2(-45.5f, 0.5f);
    }

    private static readonly ResPath BattlefieldLayout = new("/Prototypes/Imperial/Cadia/War/battlefield.txt");
    private static readonly ResPath BattlefieldUpper = new("/Prototypes/Imperial/Cadia/War/battlefield_up.txt");
    private static readonly ResPath BattlefieldLower = new("/Prototypes/Imperial/Cadia/War/battlefield_down.txt");

    // ─── Флот Хаоса на орбите ───
    // Флагман «Терзатель» — командование, батареи орбитального удара, варп-врата и десантная палуба: бойцы появляются там
    // и падают на поле когтистыми капсулами. Рядом два бомбардировщика «Адский коготь», к ним ходят варп-вратами.
    // Корабли строятся по своим планировкам (ships_gen.pl): своё питание от генераторов через SMES, подстанцию и щитки,
    // своя гравитация, двигатели, пушки и декор. Флагман стоит на якоре — ему гравитация задаётся сразу.
    // У Империума на площадке Касра — штурмовой транспорт «Валькирия», собранный так же.
    private static readonly ResPath FlagshipLayout = new("/Prototypes/Imperial/Cadia/War/chaos_flagship.txt");
    private static readonly ResPath BomberLayout = new("/Prototypes/Imperial/Cadia/War/chaos_bomber.txt");
    private static readonly ResPath ValkyrieLayout = new("/Prototypes/Imperial/Cadia/War/cadia_valkyrie.txt");
    private static readonly Vector2 FlagshipAt = new(100f, 0f);
    private static readonly Vector2[] Bombers = { new(106f, -38f), new(106f, 38f) };
    private static readonly ResPath FighterLayout = new("/Prototypes/Imperial/Cadia/War/cadia_thunderbolt.txt");
    private static readonly Vector2[] Fighters = { new(-20f, -14f), new(-20f, 14f) };

    /// <summary>Корабли, собранные по планировкам: после инициализации карт им подаётся воздух.</summary>
    private readonly List<EntityUid> _builtShips = new();

    private void ChaosFleet(IConsoleShell shell, MapId orbitId, EntityUid orbit, Func<string, Tile> ground, System.Random random)
    {
        var flagship = BuildShip(shell, orbitId, orbit, FlagshipLayout, FlagshipAt, Loc.GetString("cadia-chaos-flagship-name"), ground, random, CadiaFaction.Chaos);
        HoldGravity(flagship);
        for (var i = 0; i < Bombers.Length; i++)
        {
            BuildShip(shell, orbitId, orbit, BomberLayout, Bombers[i],
                Loc.GetString("cadia-chaos-bomber-name", ("number", i + 1)), ground, random, CadiaFaction.Chaos);
        }
    }

    /// <summary>Истребители флота «Тандерболт» у крейсера: перехват бомбардировщиков Хаоса над полем.</summary>
    private void ImperialFighters(IConsoleShell shell, MapId orbitId, EntityUid orbit, Func<string, Tile> ground, System.Random random)
    {
        for (var i = 0; i < Fighters.Length; i++)
        {
            BuildShip(shell, orbitId, orbit, FighterLayout, Fighters[i],
                Loc.GetString("cadia-fighter-name", ("number", i + 1)), ground, random, CadiaFaction.Imperium);
        }
    }

    /// <summary>
    /// Воздух на всей палубе сетки. У только что построенной сетки атмосфера тайлов ещё не создана, и подправлять её смеси
    /// бесполезно — их нет. Атмосфера пересобирается целиком, как это делает fixgridatmos: каждый тайл получает 101 кПа.
    /// </summary>
    private void FillAir(EntityUid grid)
    {
        if (!EntityManager.TryGetComponent<MapGridComponent>(grid, out var gridComp))
            return;
        var gridAtmos = EntityManager.EnsureComponent<Content.Shared.Atmos.Components.GridAtmosphereComponent>(grid);
        EntityManager.EnsureComponent<Content.Shared.Atmos.Components.GasTileOverlayComponent>(grid);
        _atmos.RebuildGridAtmosphere((grid, gridAtmos, gridComp));
    }

    /// <summary>Корабль: новая сетка на карте, собранная по файлу планировки.</summary>
    private EntityUid BuildShip(IConsoleShell shell, MapId mapId, EntityUid map, ResPath layout, Vector2 at, string name,
        Func<string, Tile> ground, System.Random random, CadiaFaction faction)
    {
        var ship = _mapManager.CreateGridEntity(map);
        EntityManager.EnsureComponent<CadiaShipFactionComponent>(ship).Faction = faction;
        // Карта — этаж стопки: без этого Z-уровни переставят пустую сетку на соседнюю при первом тайле.
        _z.KeepPlacement(ship);
        _meta.SetEntityName(ship, name);
        _xform.SetCoordinates(ship, new EntityCoordinates(map, at));
        void OnShip(string proto, float x, float y) =>
            EntityManager.SpawnEntity(proto, new EntityCoordinates(ship, new Vector2(x + 0.5f, y + 0.5f)));
        BuildBattlefield(shell, layout, ship, mapId, ship.Comp, ground, OnShip, random);
        _builtShips.Add(ship);
        return ship;
    }

    /// <summary>
    /// Проверка переходов между этажами: каждая последняя ступень — что мешает подъёму, каждый трап — связан ли он.
    /// Затем пробный подъём: тестовый моб ставится на первую исправную ступень и поднимается по ней.
    /// </summary>
    private void ZCheck(IConsoleShell shell)
    {
        var stairsSystem = EntityManager.System<ZStairsSystem>();
        int ok = 0, bad = 0, lonely = 0, linked = 0;
        Entity<ZStairsComponent>? sample = null;
        var stairs = EntityManager.EntityQueryEnumerator<ZStairsComponent, TransformComponent>();
        while (stairs.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!stairsSystem.IsTerminator((uid, comp)))
                continue;
            var reason = stairsSystem.GetBlockReason((uid, comp), out var target);
            var at = _xform.GetMapCoordinates(uid, xform);
            if (reason == null)
            {
                ok++;
                sample ??= (uid, comp);
                if (comp.Top == null)
                    shell.WriteLine($"stairs {uid} {at}: no top marker");
                continue;
            }
            bad++;
            shell.WriteLine($"stairs {uid} {at} dir {stairsSystem.GetDir(uid)} -> {target}: {reason}");
        }
        var ladders = EntityManager.EntityQueryEnumerator<ZLadderComponent, TransformComponent>();
        while (ladders.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Up == null && comp.Down == null)
            {
                lonely++;
                shell.WriteLine($"ladder {uid} {_xform.GetMapCoordinates(uid, xform)}: not linked");
            }
            else
            {
                linked++;
            }
        }
        shell.WriteLine($"zcheck: stairs ok {ok}, blocked {bad}; ladders linked {linked}, lonely {lonely}");

        if (sample is not { } probe)
            return;
        var start = _xform.GetMapCoordinates(probe);
        var mob = EntityManager.SpawnEntity("MobHuman", start);
        var moved = stairsSystem.StairAscend(probe, mob);
        shell.WriteLine($"zcheck: ascend from {start} -> {moved} now at {_xform.GetMapCoordinates(mob)}");
        EntityManager.QueueDeleteEntity(mob);
    }

    /// <summary>На орбите гравитации нет, а генератора у флагмана нет: палубу держим своей.</summary>
    private void HoldGravity(EntityUid grid)
    {
        var gravity = EntityManager.EnsureComponent<GravityComponent>(grid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        EntityManager.Dirty(grid, gravity);
    }

    /// <summary>Этаж стопки поля: своя карта с воздухом, гравитацией и светом, построенная по файлу планировки.</summary>
    private EntityUid BuildFloor(IConsoleShell shell, string stack, int level, ResPath layout, float[] moles, Color light,
        Func<string, Tile> ground, System.Random random)
    {
        var floor = _map.CreateMap(out var floorId, false);
        _meta.SetEntityName(floor, Loc.GetString(level > 0 ? "cadia-floor-upper" : "cadia-floor-lower", ("field", stack)));
        var grid = EntityManager.EnsureComponent<MapGridComponent>(floor);
        var gravity = EntityManager.EnsureComponent<GravityComponent>(floor);
        gravity.Enabled = true;
        gravity.Inherent = true;
        EntityManager.Dirty(floor, gravity);
        var mapLight = EntityManager.EnsureComponent<MapLightComponent>(floor);
        mapLight.AmbientLightColor = light;
        EntityManager.Dirty(floor, mapLight);
        _atmos.SetMapAtmosphere(floor, false, new GasMixture(moles, Atmospherics.T20C));

        void Place(string proto, float x, float y) =>
            EntityManager.SpawnEntity(proto, new EntityCoordinates(floor, new Vector2(x + 0.5f, y + 0.5f)));
        BuildBattlefield(shell, layout, floor, floorId, grid, ground, Place, random);
        _z.SetLevel(floor, stack, level);
        _map.InitializeMap(floorId);
        // Снег идёт и над стенами, а в катакомбах его нет.
        if (level > 0)
            LetItSnow(floorId);
        return floor;
    }
    /// <summary>
    /// Сетка battlefield.txt: тайл и что стоит на клетке. Остальные объекты и декали задаются строками '*' и '>'.
    /// </summary>
    private static readonly Dictionary<char, (string Tile, string? Proto)> Legend = new()
    {
        ['.'] = ("CadiaFloorTundra", null), [','] = ("CadiaFloorMud", null), ['~'] = ("CadiaFloorAsh", null),
        [':'] = ("CadiaFloorScorched", null), ['='] = ("CadiaFloorRoad", null), ['_'] = ("CadiaFloorRockcrete", null),
        ['f'] = ("CadiaFloorFlagstone", null), ['b'] = ("CadiaFloorBasalt", null), ['h'] = ("CadiaDeckHazard", null),
        ['g'] = ("CadiaDeckGothic", null), ['q'] = ("CadiaDeckRune", null), ['u'] = ("CadiaDeckFlesh", null),
        ['#'] = ("CadiaFloorFlagstone", "CadiaWallReinforced"), ['w'] = ("CadiaFloorRockcrete", "CadiaWall"),
        ['r'] = ("CadiaFloorRockcrete", "CadiaWall"), ['R'] = ("CadiaFloorRockcrete", "CadiaRubble"),
        ['t'] = ("CadiaFloorMud", "CadiaTrench"), ['s'] = ("CadiaFloorTundra", "CadiaSandbags"), ['x'] = ("CadiaFloorMud", "CadiaRazorWire"),
        ['k'] = ("CadiaFloorMud", "CadiaTankTrap"), ['y'] = ("CadiaFloorAsh", "CadiaSpikeBarricade"),
        ['P'] = ("CadiaFloorBasalt", null), ['@'] = ("CadiaFloorRockcrete", null),
        ['c'] = ("CadiaDeckCarpet", null), ['o'] = ("CadiaFloorTundra", "CadiaRock"),
        ['W'] = ("CadiaFloorRockcrete", "CadiaReinforcedWindow"), ['D'] = ("CadiaFloorRockcrete", "CadiaWoodDoor"), ['M'] = ("CadiaFloorRockcrete", "CadiaMetalDoor"),
        ['O'] = ("CadiaFloorTundra", "CadiaBedrock"), ['B'] = ("CadiaFloorFlagstone", "CadiaWallCatacomb"),
        ['X'] = ("CadiaDeckChaos", "WallPlastitanium"), ['V'] = ("CadiaDeckChaos", "PlastitaniumWindow"), ['K'] = ("CadiaDeckChaos", null),
        ['E'] = ("CadiaDeckEngine", null), ['G'] = ("CadiaDeckGrate", null),
        ['N'] = ("CadiaDeckNavy", null), ['Y'] = ("CadiaDeckNavy", "WallReinforced"), ['Z'] = ("CadiaDeckNavy", "ReinforcedWindow"),
    };

    /// <summary>
    /// Боевая карта на 100 игроков по battlefield.txt: сетка земли и стен, затем строки "* столбец строка Прототип [поворот]"
    /// и "> столбец строка декаль [поворот]". Возвращает, куда поставить ведущего.
    /// </summary>
    private Vector2 BuildBattlefield(IConsoleShell shell, ResPath layout, EntityUid mapUid, MapId mapId, MapGridComponent grid, Func<string, Tile> ground,
        Action<string, float, float> place, System.Random random)
    {
        var lines = _res.ContentFileReadAllText(layout).Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => !l.StartsWith(';')).ToList();
        // Строки объектов, декалей и крыш — не сетка: иначе поле выше этажей и съезжает относительно них.
        var rows = lines.Where(l => !l.StartsWith("* ") && !l.StartsWith("> ") && !l.StartsWith("^ ")).ToList();
        var height = rows.Count;
        var width = rows.Max(r => r.Length);
        var left = -width / 2;
        var top = height / 2;
        Vector2 World(int col, int row) => new(left + col, top - row);
        // Поле на своей карте (не корабль): края для бурана и для границы, по которой сервер не выпускает наружу.
        if (EntityManager.HasComponent<MapComponent>(mapUid))
        {
            var bounds = EntityManager.EnsureComponent<CadiaFieldBoundsComponent>(mapUid);
            bounds.Bounds = new Box2(left, top - height + 1, left + width, top + 1);
            EntityManager.Dirty(mapUid, bounds);
        }

        var tiles = new List<(Vector2i, Tile)>(width * height);
        var unknown = new HashSet<char>();
        var pylons = new List<Vector2>();
        Vector2? shuttle = null;
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var c = col < rows[row].Length ? rows[row][col] : '.';
                // '0' — пустота: на верхнем этаже проём на поле, на нижнем — толща земли.
                if (c == '0')
                    continue;
                if (!Legend.TryGetValue(c, out var entry))
                {
                    unknown.Add(c);
                    entry = Legend['.'];
                }
                var at = World(col, row);
                tiles.Add((new Vector2i((int) at.X, (int) at.Y), ground(entry.Tile)));
                if (c == 'P')
                    pylons.Add(at);
                else if (c == '@')
                    shuttle = at;
            }
        }
        _map.SetTiles(mapUid, grid, tiles);
        if (unknown.Count > 0)
            shell.WriteError($"cadia map: unknown layout symbols {string.Join(' ', unknown)}");

        // Стены и заграждения из сетки ставятся после всех тайлов, чтобы сглаживание стен видело соседей.
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < rows[row].Length; col++)
            {
                if (Legend.TryGetValue(rows[row][col], out var entry) && entry.Proto is { } proto)
                {
                    var at = World(col, row);
                    place(proto, at.X, at.Y);
                }
            }
        }

        // Пилоны с запада на восток: I у крепости, последний у плацдарма Хаоса.
        pylons.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : b.Y.CompareTo(a.Y));
        for (var i = 0; i < pylons.Count; i++)
        {
            var pylon = EntityManager.SpawnEntity("CadiaPylon", new EntityCoordinates(mapUid, pylons[i] + new Vector2(0.5f)));
            EntityManager.GetComponent<CadiaPylonComponent>(pylon).Number = i + 1;
        }

        Vector2? arrival = null;
        var objects = 0;
        var decals = 0;
        var roofs = 0;
        var failed = new HashSet<string>();
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !int.TryParse(parts[1], out var col) || !int.TryParse(parts[2], out var row))
                continue;
            var at = World(col, row);
            var rotation = parts.Length >= 5 && float.TryParse(parts[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var degrees)
                ? Angle.FromDegrees(degrees)
                : Angle.Zero;
            if (parts[0] == "^")
            {
                // Крыша над зданием: прямоугольник "x0 y0 x1 y1" в клетках сетки.
                if (parts.Length < 5 || !int.TryParse(parts[3], out var col1) || !int.TryParse(parts[4], out var row1))
                    continue;
                var roof = EntityManager.EnsureComponent<Content.Shared.Light.Components.RoofComponent>(mapUid);
                for (var r = row; r <= row1; r++)
                {
                    for (var c = col; c <= col1; c++)
                    {
                        var cell = World(c, r);
                        _roof.SetRoof((mapUid, grid, roof), new Vector2i((int) cell.X, (int) cell.Y), true);
                    }
                }
                roofs++;
                continue;
            }
            if (parts[0] == "*")
            {
                if (!_proto.HasIndex<Robust.Shared.Prototypes.EntityPrototype>(parts[3]))
                {
                    failed.Add(parts[3]);
                    continue;
                }
                var uid = EntityManager.SpawnEntity(parts[3], new EntityCoordinates(mapUid, at + new Vector2(0.5f)));
                if (rotation != Angle.Zero)
                    _xform.SetLocalRotation(uid, rotation);
                if (parts[3] == "CadiaSpawnPointImperium")
                    arrival ??= at + new Vector2(0.5f);
                objects++;
            }
            else if (parts[0] == ">")
            {
                if (_decals.TryAddDecal(parts[3], new EntityCoordinates(mapUid, at), out _, rotation: rotation, cleanable: false))
                    decals++;
                else
                    failed.Add(parts[3]);
            }
        }
        if (failed.Count > 0)
            shell.WriteError($"cadia map: not placed {string.Join(", ", failed)}");

        if (shuttle is { } pad)
            BuildShip(shell, mapId, mapUid, ValkyrieLayout, pad, Loc.GetString("cadia-valkyrie-name"), ground, random, CadiaFaction.Imperium);

        shell.WriteLine(Loc.GetString("cmd-cadia-map-built", ("width", width), ("height", height), ("pylons", pylons.Count),
            ("objects", objects), ("decals", decals)));
        return arrival ?? Vector2.Zero;
    }
}
