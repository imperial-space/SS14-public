using System.Numerics;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

public sealed class IslandRadialGenerationSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IslandRadialGenerationComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, IslandRadialGenerationComponent comp, MapInitEvent args)
    {
        var mapId = Transform(uid).MapID;
        if (mapId == MapId.Nullspace)
            return;

        SpawnIslands(mapId, comp);
    }

    public void SpawnIslands(MapId mapId, IslandRadialGenerationComponent config)
    {
        var rng = new Random(_random.Next());

        var lowPool   = BuildPool(config.LowIslands);
        var medPool   = BuildPool(config.MediumIslands);
        var highPool  = BuildPool(config.HighIslands);

        var maxR = 0f;
        foreach (var (_, r) in lowPool) maxR = MathF.Max(maxR, r);
        foreach (var (_, r) in medPool) maxR = MathF.Max(maxR, r);
        foreach (var (_, r) in highPool) maxR = MathF.Max(maxR, r);

        var cellSize = maxR > 0f ? maxR + config.InterIslandsThreshold : config.InterIslandsThreshold;
        var grid = new IslandSpatialGrid(cellSize);
        var gen  = new IslandBridsonGenerator(config.InterIslandsThreshold, config.MaxCandidatesPerPoint);

        var placements = new List<IslandPlacement>();
        placements.AddRange(gen.Generate(new IslandRing(config.LowIslandMinRange,    config.MediumIslandMinRange), lowPool,  grid, rng));
        placements.AddRange(gen.Generate(new IslandRing(config.MediumIslandMinRange, config.HighIslandMinRange),   medPool,  grid, rng));
        placements.AddRange(gen.Generate(new IslandRing(config.HighIslandMinRange,   config.HighIslandMaxRange),   highPool, grid, rng));

        foreach (var placement in placements)
        {
            _mapLoader.TryLoadGrid(mapId, placement.Path, out _, offset: placement.Pos);
        }
    }

    private List<(ResPath Path, float Radius)> BuildPool(List<ResPath> paths)
    {
        var pool = new List<(ResPath, float)>(paths.Count);
        foreach (var path in paths)
        {
            var radius = IslandRadiusParser.TryComputeRadius(path, _res);
            if (radius == null)
            {
                Log.Warning($"Could not compute radius for island {path}, skipping.");
                continue;
            }
            pool.Add((path, radius.Value));
        }
        return pool;
    }
}
