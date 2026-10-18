using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Security;

/// <summary>
/// В SS13 трудовой лагерь стоит на Лаваленде. Лаваленд в сборке ивентовый, поэтому если маяка
/// гулага нет (Лаваленд не запущен), лагерь по первому запросу консоли загружается на отдельную
/// лавовую планету с рудой — без мегафауны и руин.
/// </summary>
public sealed class LaborCampMapSystem : EntitySystem
{
    private const string CampMap = "/Maps/Imperial/Lavaland/labour_camp.yml";
    private const string BiomeTemplate = "ImperialLavaland";

    private static readonly string[] OreLayers =
    {
        "OreIron", "OreQuartz", "OreCoal", "OreGold", "OreSilver", "OrePlasma", "OreUranium", "OreDiamond",
    };

    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>Грид лагеря, которому ещё нужно перестроить атмосферу.</summary>
    private EntityUid? _pendingAtmos;

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

    /// <summary>Есть ли маяк; если нет — загрузить лагерь.</summary>
    public bool EnsureCamp()
    {
        var query = EntityQueryEnumerator<GulagBeaconComponent>();
        if (query.MoveNext(out _, out _))
            return true;

        return LoadCamp();
    }

    private bool LoadCamp()
    {
        var mapUid = _maps.CreateMap(out var mapId, runMapInit: false);
        _meta.SetEntityName(mapUid, Loc.GetString("labor-camp-map-name"));

        if (_proto.TryIndex<BiomeTemplatePrototype>(BiomeTemplate, out var biomeTemplate))
        {
            _biome.EnsurePlanet(mapUid, biomeTemplate, _random.Next(), null, Color.FromHex("#80351A"));

            var biome = Comp<BiomeComponent>(mapUid);
            foreach (var layer in OreLayers)
            {
                _biome.AddMarkerLayer(mapUid, biome, layer);
            }
        }

        // datum/atmosphere/lavaland: мало кислорода, много углекислого газа.
        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = 5f;
        moles[(int) Gas.CarbonDioxide] = 77.10312f;
        _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_loader.TryLoadGrid(mapId, new ResPath(CampMap), out var grid, opts, Vector2.Zero) || grid == null)
        {
            Log.Error("Не удалось загрузить карту трудового лагеря.");
            QueueDel(mapUid);
            return false;
        }

        try
        {
            _maps.InitializeMap(mapId);
        }
        catch (Exception e)
        {
            Log.Error($"Ошибка при инициализации карты трудового лагеря: {e}");
            _maps.SetPaused(mapUid, false);
        }

        RemComp<LightCycleComponent>(mapUid);
        RemComp<SunShadowCycleComponent>(mapUid);
        _pendingAtmos = grid.Value.Owner;
        return true;
    }
}
