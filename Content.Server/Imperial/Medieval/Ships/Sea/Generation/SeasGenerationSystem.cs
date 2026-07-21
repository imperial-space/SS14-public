using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Medieval.Ships.Sea.Init;
using Content.Shared.Atmos;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Parallax;
using Content.Shared.Light.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Sea.Generation;

public sealed class SeasGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    private const int MapMin = -75;
    private const int MapMax = 75;

    private static readonly (string PrototypeId, int Count)[] IslandConfig = {
        ("PirateIslands", 1),
        ("FrendlyIslands", 2),
        ("VolcanicIsland", 10)
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<SeasGenerationEvent>(OnSeasGeneration);
    }

    public void EnsureSeasGenerated(SeasGenerationStateComponent component)
    {
        if (component.SeaMatrix == null)
            component.SeaMatrix = new SeaMatrix(new List<(int x, int y)>
            {
                (2, 2), (2, 3), (2, 4),
                (3, 2), (3, 3), (3, 4),
                (4, 2), (4, 3), (4, 4),
            });

        if (component.SeaInitialized)
            return;

        var seaMatrix = component.SeaMatrix;

        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                if (!seaMatrix.NeedsGeneration(x, y))
                    continue;

                var mapUid = _map.CreateMap();
                _metaData.SetEntityName(mapUid, $"Sea {x} {y}");

                var gravity = EnsureComp<GravityComponent>(mapUid);
                gravity.Enabled = true;
                gravity.Inherent = true;
                Dirty(mapUid, gravity);

                var light = EnsureComp<MapLightComponent>(mapUid);
                light.AmbientLightColor = Color.FromHex("#D8B059");
                Dirty(mapUid, light);

                EnsureComp<LightCycleComponent>(mapUid);
                var moles = new float[Atmospherics.AdjustedNumberOfGases];
                moles[(int) Gas.Oxygen] = 21.824779f;
                moles[(int) Gas.Nitrogen] = 82.10312f;
                var mixture = new GasMixture(moles, Atmospherics.T20C);
                _atmos.SetMapAtmosphere(mapUid, false, mixture);

                var sea = AddComp<SeaComponent>(mapUid);
                var parallax = AddComp<ParallaxComponent>(mapUid);
                parallax.Parallax = sea.CalmParallax;
                var mapId = _transform.GetMapId(mapUid);
                seaMatrix.SetSeaId(x, y, mapId);
                seaMatrix.SetGenerated(x, y, false);
            }
        }

        GenerateIslandsOnSeaMaps(seaMatrix);

        component.SeaInitialized = true;
    }

    private void GenerateIslandsOnSeaMaps(SeaMatrix seaMatrix)
    {
        var generatedObjects = new List<EntityUid>();
        var occupiedTiles = new HashSet<(int X, int Y)>();

        var seaMapIds = new List<MapId>();
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                var cell = seaMatrix.GetCell(x, y);
                if (!cell.NeedGenerate && !(cell.SeaId == new MapId(-1)))
                    seaMapIds.Add(cell.SeaId);
            }
        }

        if (seaMapIds.Count == 0)
        {
            Logger.Warning("No sea maps found to generate islands on!");
            return;
        }

        foreach (var (prototypeId, count) in IslandConfig)
        {
            if (!_prototypeManager.TryIndex<IslandPrototype>(prototypeId, out var prototype) || prototype.Path == null)
            {
                Logger.Warning($"Island prototype '{prototypeId}' not found! Skipping.");
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                int attempts = 0;
                const int maxAttempts = 100;

                while (++attempts <= maxAttempts)
                {
                    var targetMapId = seaMapIds[_random.Next(seaMapIds.Count)];

                    int x = _random.Next(MapMin, MapMax - prototype.Size + 1);
                    int y = _random.Next(MapMin, MapMax - prototype.Size + 1);

                    bool overlaps = false;
                    var newTiles = new List<(int X, int Y)>();

                    for (int dx = 0; dx < prototype.Size; dx++)
                    {
                        for (int dy = 0; dy < prototype.Size; dy++)
                        {
                            var tile = (x + dx, y + dy);
                            if (occupiedTiles.Contains(tile))
                            {
                                overlaps = true;
                                break;
                            }
                            newTiles.Add(tile);
                        }
                        if (overlaps)
                            break;
                    }

                    if (!overlaps)
                    {
                        _mapLoader.TryLoadGrid(targetMapId, new ResPath(prototype.Path), out var newObj, offset: new Vector2(x,y));
                        if (newObj.HasValue)
                        {
                            generatedObjects.Add(newObj.Value);
                            foreach (var tile in newTiles)
                                occupiedTiles.Add(tile);
                            break;
                        }
                    }
                }

                if (attempts > maxAttempts)
                {
                    Logger.Warning($"Failed to generate {prototypeId} after {maxAttempts} attempts.");
                }
            }
        }

        Logger.Info($"Successfully generated {generatedObjects.Count} islands across {seaMapIds.Count} sea maps.");
    }

    public sealed class SeasGenerationEvent
    {
        public MapId MapId { get; set; }
        public int Count { get; set; }
        public string Prototype { get; set; } = "Reef";
    }

    private void OnSeasGeneration(SeasGenerationEvent ev)
    {
    }
}
