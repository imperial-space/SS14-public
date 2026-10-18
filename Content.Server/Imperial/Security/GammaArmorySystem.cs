using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Materials;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>Гамма-автолат: предзагружен 50 листами стали и стекла (autolathe/loaded/upgraded/gamma).</summary>
[RegisterComponent]
public sealed partial class GammaAutolatheComponent : Component
{
    [DataField]
    public Dictionary<string, int> StartingMaterials = new() { ["Steel"] = 5000, ["Glass"] = 5000 };
}

/// <summary>
/// Шаттл гамма-оружейки из ParadiseSS13 (move_gamma_ship): ЦК присылает к станции оружейку
/// с особым снаряжением и может отозвать её обратно. Шаттл собирается кодом при первом вызове.
/// </summary>
public sealed class GammaArmorySystem : EntitySystem
{
    /// <summary>mecha/combat/marauder/ares/loaded: есть только в закрытой сборке, поэтому спавнится по наличию прототипа.</summary>
    private const string MechProto = "MechMarauder";

    private static readonly SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Imperial/Security/gamma_deploy.ogg");
    private static readonly SoundSpecifier RecallSound = new SoundPathSpecifier("/Audio/Imperial/Security/gamma_recall.ogg");

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly Content.Server.Decals.DecalSystem _decals = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materials = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ITileDefinitionManager _tiles = default!;

    private EntityUid? _shuttleGrid;
    private EntityUid? _homeMap;
    private bool _deployed;
    private EntityUid? _pendingAtmos;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GammaAutolatheComponent, MapInitEvent>(OnAutolatheInit);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _shuttleGrid = null;
            _homeMap = null;
            _deployed = false;
            _pendingAtmos = null;
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingAtmos is not { } grid)
            return;

        if (!TryComp<GridAtmosphereComponent>(grid, out var gridAtmos) || !TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            _pendingAtmos = null;
            return;
        }

        if (gridAtmos.Tiles.Count == 0)
            return;

        _atmos.RebuildGridAtmosphere((grid, gridAtmos, mapGrid));
        _pendingAtmos = null;
    }

    private void OnAutolatheInit(Entity<GammaAutolatheComponent> ent, ref MapInitEvent args)
    {
        foreach (var (material, amount) in ent.Comp.StartingMaterials)
        {
            _materials.TryChangeMaterialAmount(ent, material, amount);
        }
    }

    /// <summary>move_gamma_ship(): прислать оружейку к станции или отозвать на ЦК.</summary>
    public string Toggle()
    {
        if (_shuttleGrid == null || TerminatingOrDeleted(_shuttleGrid.Value))
        {
            _shuttleGrid = BuildShuttle();
            _deployed = false;
        }

        var grid = _shuttleGrid.Value;
        if (!TryComp<ShuttleComponent>(grid, out var shuttle))
            return Loc.GetString("gamma-armory-error");

        if (HasComp<FTLComponent>(grid))
            return Loc.GetString("gamma-armory-in-transit");

        if (!_deployed)
        {
            if (FindStationGrid() is not { } station)
                return Loc.GetString("gamma-armory-no-station");

            _shuttle.FTLToDock(grid, shuttle, station);
            _deployed = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("gamma-armory-deploy"), Loc.GetString("gamma-armory-sender"),
                announcementSound: DeploySound, colorOverride: Color.FromHex("#d6d0ff"));
            return Loc.GetString("gamma-armory-sent");
        }

        if (_homeMap is not { } home || TerminatingOrDeleted(home))
            return Loc.GetString("gamma-armory-error");

        _shuttle.FTLToCoordinates(grid, shuttle, new EntityCoordinates(home, Vector2.Zero), Angle.Zero);
        _deployed = false;
        _chat.DispatchGlobalAnnouncement(Loc.GetString("gamma-armory-recall"), Loc.GetString("gamma-armory-sender"),
            announcementSound: RecallSound, colorOverride: Color.FromHex("#d6d0ff"));
        return Loc.GetString("gamma-armory-recalled");
    }

    private EntityUid? FindStationGrid()
    {
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var station, out _))
        {
            if (_station.GetLargestGrid(station) is { } grid)
                return grid;
        }

        return null;
    }

    #region Постройка шаттла

    private const int Width = 13;
    private const int Height = 9;

    /// <summary>gamma_armory_base.dmm: бронированный отсек с оружейными сейфами, РИГом, автолатом и мехом.</summary>
    private EntityUid BuildShuttle()
    {
        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString("gamma-armory-map-name"));
        _homeMap = mapUid;

        var gridEnt = _mapManager.CreateGridEntity(mapId);
        var grid = gridEnt.Owner;
        _meta.SetEntityName(grid, Loc.GetString("gamma-armory-shuttle-name"));

        var floor = new Tile(_tiles["FloorShuttleWhite"].TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                tiles.Add((new Vector2i(x, y), floor));
            }
        }

        _map.SetTiles(gridEnt, tiles);

        // Корпус и стыковочный шлюз на западной стене.
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                if (!border)
                    continue;

                if (x == 0 && y == Height / 2)
                {
                    SpawnAt(grid, "AirlockGlassShuttle", x, y, Angle.FromDegrees(90));
                    continue;
                }

                // Иллюминаторы в южной и кормовой стенах.
                var window = (y == 0 && (x == 4 || x == 8)) || (x == Width - 1 && (y == 2 || y == 6));
                SpawnAt(grid, window ? "ShuttleWindow" : "WallShuttle", x, y);
            }
        }

        DecorateFloor(gridEnt);

        // Пять оружейных шкафов вдоль северной стены.
        SpawnAt(grid, "GunSafeGammaNuclear", 2, 7);
        SpawnAt(grid, "GunSafeGammaImmolator", 3, 7);
        SpawnAt(grid, "GunSafeGammaSaber", 4, 7);
        SpawnAt(grid, "GunSafeGammaShotgun", 5, 7);
        SpawnAt(grid, "GunSafeGammaSupport", 6, 7);

        // Хранилище гамма-РИГа и гамма-автолат.
        SpawnAt(grid, "ModSuitStorageGamma", 8, 7);
        SpawnAt(grid, "GammaAutolathe", 10, 7);

        // Столы с гранатами, материалами и двумя зарядниками.
        for (var x = 2; x <= 9; x++)
        {
            SpawnAt(grid, "TableReinforced", x, 1);
        }

        SpawnAt(grid, "ClusterBangFull", 2, 1);
        SpawnAt(grid, "GrenadeIncendiary", 3, 1);
        SpawnStack(grid, "SheetSteel", 4, 1);
        SpawnStack(grid, "SheetGlass", 5, 1);
        SpawnAt(grid, "GammaWeaponRecharger", 7, 1);
        SpawnAt(grid, "GammaWeaponRecharger", 8, 1);

        if (_proto.HasIndex<EntityPrototype>(MechProto))
            SpawnAt(grid, MechProto, 9, 4);

        // Свет.
        SpawnAt(grid, "AlwaysPoweredWallLight", 4, 6);
        SpawnAt(grid, "AlwaysPoweredWallLight", 8, 6);
        SpawnAt(grid, "AlwaysPoweredWallLight", 6, 2, Angle.FromDegrees(180));

        // Отделка: воздушная завеса в шлюзе, таблички, дисплеи статуса и двигатели на корме.
        SpawnAt(grid, "AtmosDeviceFanTiny", 0, Height / 2);
        SpawnAt(grid, "SignSpace", 0, Height / 2 + 1);
        SpawnAt(grid, "SignSecurearea", 0, Height / 2 - 1);
        SpawnAt(grid, "Screen", 5, Height - 1);
        SpawnAt(grid, "Screen", 9, Height - 1);
        SpawnAt(grid, "SignArmory", 7, Height - 1);

        _map.SetTiles(gridEnt, new List<(Vector2i, Tile)>
        {
            (new Vector2i(Width, 3), floor),
            (new Vector2i(Width, 5), floor),
        });
        SpawnAt(grid, "Thruster", Width, 3, Angle.FromDegrees(90));
        SpawnAt(grid, "Thruster", Width, 5, Angle.FromDegrees(90));

        // Настенное: огнетушитель, интерком, плакаты СБ.
        SpawnAt(grid, "ExtinguisherCabinetFilled", Width - 1, Height / 2, Angle.FromDegrees(90));
        SpawnAt(grid, "IntercomSecurity", 0, Height / 2 + 2, Angle.FromDegrees(-90));
        SpawnAt(grid, "PosterLegitNanotrasenLogo", 3, Height - 1);
        SpawnAt(grid, "PosterLegitReportCrimes", 11, Height - 1);
        SpawnAt(grid, "PosterLegitObey", 2, 0, Angle.FromDegrees(180));
        SpawnAt(grid, "PosterLegitHereForYourSafety", 10, 0, Angle.FromDegrees(180));

        // Растения по углам и стеллаж с фонарями СБ.
        SpawnAt(grid, "PottedPlantRandom", 1, 1);
        SpawnAt(grid, "PottedPlantRandom", Width - 2, Height - 2);
        SpawnAt(grid, "Rack", 1, Height - 2);
        SpawnAt(grid, "FlashlightSeclite", 1, Height - 2);
        SpawnAt(grid, "FlashlightSeclite", 1, Height - 2);

        // Зарядка мехов (mech_bay_recharge_port): зарядник силовых ячеек и две запасные ячейки.
        SpawnAt(grid, "GammaPowerCageRecharger", Width - 2, 1);
        SpawnAt(grid, "PowerCageHigh", 6, 1);
        SpawnAt(grid, "PowerCageHigh", 6, 1);

        EnsureComp<ShuttleComponent>(grid);
        var gravity = EnsureComp<GravityComponent>(grid);
        gravity.Enabled = true;
        Dirty(grid, gravity);

        _pendingAtmos = grid;
        return grid;
    }

    private static readonly Color SecurityRed = Color.FromHex("#DE3A3A");

    /// <summary>Тёмная дорожка по центру, тёмный пол у шкафов и красная разметка.</summary>
    private void DecorateFloor(Entity<MapGridComponent> grid)
    {
        var dark = new Tile(_tiles["FloorDark"].TileId);
        var mono = new Tile(_tiles["FloorDarkMono"].TileId);
        var tiles = new List<(Vector2i, Tile)>();

        for (var x = 1; x < Width - 1; x++)
        {
            for (var y = 3; y <= 5; y++)
            {
                tiles.Add((new Vector2i(x, y), dark));
            }
        }

        for (var x = 1; x < Width - 1; x++)
        {
            tiles.Add((new Vector2i(x, Height - 2), mono));
        }

        _map.SetTiles(grid, tiles);

        // Красные линии вдоль дорожки.
        for (var x = 1; x < Width - 1; x++)
        {
            Decal(grid, "WarnLineGreyscaleN", x, 5);
            Decal(grid, "WarnLineGreyscaleS", x, 3);
        }

        // Площадка меха, места перед хранилищем РИГа и автолатом, тамбур шлюза.
        Decal(grid, "BoxGreyscale", 9, 4);
        Decal(grid, "DeliveryGreyscale", 8, Height - 3);
        Decal(grid, "DeliveryGreyscale", 10, Height - 3);
        Decal(grid, "WarnBox", 1, Height / 2, tinted: false);
    }

    private void Decal(EntityUid grid, string id, int x, int y, bool tinted = true)
    {
        _decals.TryAddDecal(id, new EntityCoordinates(grid, x, y), out _, tinted ? SecurityRed : null);
    }

    private EntityUid SpawnAt(EntityUid grid, string proto, int x, int y, Angle? rotation = null)
    {
        var ent = Spawn(proto, new EntityCoordinates(grid, x + 0.5f, y + 0.5f));
        if (rotation is { } rot)
            _xform.SetLocalRotation(ent, rot);

        return ent;
    }

    private void SpawnStack(EntityUid grid, string proto, int x, int y)
    {
        var ent = SpawnAt(grid, proto, x, y);
        if (TryComp<Content.Shared.Stacks.StackComponent>(ent, out var stack))
            EntityManager.System<Content.Server.Stack.StackSystem>().SetCount(ent, 50, stack);
    }

    #endregion
}

/// <summary>Админ-команда: прислать или отозвать гамма-оружейку.</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class GammaArmoryCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "gammaarmory";
    public string Description => "Присылает к станции шаттл гамма-оружейки или отзывает его на ЦК.";
    public string Help => Command;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(_entManager.System<GammaArmorySystem>().Toggle());
    }
}
