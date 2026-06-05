using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Lavaland.LavalandShuttle;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Server.Parallax;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Lavaland.LavalandPlanet;

public sealed class LavalandPlanetRuleSystem : GameRuleSystem<LavalandPlanetRuleComponent>
{
    [Dependency] private readonly BiomeSystem _biome = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;

    protected override void Started(EntityUid uid, LavalandPlanetRuleComponent comp, GameRuleComponent rule, GameRuleStartedEvent args)
    {
        var mapUid = _maps.CreateMap(out var mapId, runMapInit: false);
        _meta.SetEntityName(mapUid, "Лаваленд");
        EnsureComp<LavalandMapComponent>(mapUid);

        var biomeTemplate = _proto.Index<BiomeTemplatePrototype>(comp.BiomeTemplate);
        _biome.EnsurePlanet(mapUid, biomeTemplate, _random.Next(), null, comp.MapLight);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int)Gas.Nitrogen] = 82.10312f;
        _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

        var biome = Comp<BiomeComponent>(mapUid);
        foreach (var layer in comp.OreLayers)
            _biome.AddMarkerLayer(mapUid, biome, layer);
        foreach (var layer in comp.MobLayers)
            _biome.AddMarkerLayer(mapUid, biome, layer);

        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        var placedBounds = new List<Box2>();
        var structureGrids = new List<EntityUid>();

        // Load ALL structures BEFORE InitializeMap so OnBiomeMapInit sees every grid
        // and marks their tiles in biome.ModifiedTiles, preventing basalt spawning inside.
        EntityUid? recyclingOutpostGridUid = null;

        if (comp.RecyclingOutpostMap is { } recyclingPath &&
            _loader.TryLoadGrid(mapId, new ResPath(recyclingPath), out var recyclingGrid, opts, Vector2.Zero) &&
            recyclingGrid.HasValue)
        {
            recyclingOutpostGridUid = recyclingGrid.Value.Owner;
            placedBounds.Add(recyclingGrid.Value.Comp.LocalAABB.Enlarged(5f));
            structureGrids.Add(recyclingOutpostGridUid.Value);
        }

        if (comp.PrisonMap is { } prisonPath &&
            _loader.TryLoadGrid(mapId, new ResPath(prisonPath), out var prisonGrid, opts, new Vector2(30f, 0f)) &&
            prisonGrid.HasValue)
        {
            placedBounds.Add(prisonGrid.Value.Comp.LocalAABB.Translated(new Vector2(30f, 0f)).Enlarged(5f));
            structureGrids.Add(prisonGrid.Value.Owner);
        }

        foreach (var gridPath in comp.StructureGridFiles)
        {
            if (!_loader.TryLoadGrid(mapId, new ResPath(gridPath), out var gridEnt, opts, new Vector2(5000f, 0f)) || !gridEnt.HasValue)
                continue;

            var localBounds = gridEnt.Value.Comp.LocalAABB;
            var pos = FindSafePosition(localBounds, placedBounds, comp);

            _xform.SetWorldPosition(gridEnt.Value.Owner, pos);
            placedBounds.Add(localBounds.Translated(pos).Enlarged(5f));
            structureGrids.Add(gridEnt.Value.Owner);
        }

        foreach (var mapPath in comp.StructureMapFiles)
        {
            if (!_loader.TryLoadMap(new ResPath(mapPath), out var loadedMap, out var grids, opts))
                continue;

            var pos = FindSafePosition(Box2.UnitCentered, placedBounds, comp);

            if (grids != null)
            {
                foreach (var grid in grids)
                {
                    _xform.SetParent(grid.Owner, mapUid);
                    _xform.SetWorldPosition(grid.Owner, pos);
                    structureGrids.Add(grid.Owner);
                }
            }

            if (loadedMap.HasValue)
                QueueDel(loadedMap.Value.Owner);
        }

        // Load shuttle BEFORE InitializeMap so OnBiomeMapInit reserves its tiles.
        EntityUid? shuttleGridUid = null;
        if (comp.ShuttleMap is { } shuttlePath)
        {
            if (_loader.TryLoadGrid(mapId, new ResPath(shuttlePath), out var shuttleGrid, opts, new Vector2(5000f, 0f)) &&
                shuttleGrid.HasValue)
            {
                var localBounds = shuttleGrid.Value.Comp.LocalAABB;
                var safePos = FindSafePosition(localBounds, placedBounds, comp);
                _xform.SetWorldPosition(shuttleGrid.Value.Owner, safePos);
                placedBounds.Add(localBounds.Translated(safePos).Enlarged(5f));
                shuttleGridUid = shuttleGrid.Value.Owner;
                structureGrids.Add(shuttleGridUid.Value);
            }
        }

        var planetGrid = Comp<MapGridComponent>(mapUid);
        var basaltTile = new Tile(_tileDefManager["FloorBasalt"].TileId);

        _maps.InitializeMap(mapId);

        // Remove day/night cycle: Lavaland has fixed lighting, no oscillation.
        // Must be done after InitializeMap so OnCycleShutdown restores the correct base color.
        RemComp<LightCycleComponent>(mapUid);
        RemComp<SunShadowCycleComponent>(mapUid);

        // Defer atmosphere rebuild: at this point GridAtmosphereComponent.Tiles is empty
        // (tiles are added by the atmos system on the first update tick). We queue the grids
        // and RebuildGridAtmosphere is called in Update once Tiles are populated.
        comp.PendingAtmosRebuild.AddRange(structureGrids);

        // After map init: wire console regardless of whether shuttle loaded.
        if (recyclingOutpostGridUid.HasValue)
        {
            var consoleQuery = EntityQueryEnumerator<LavalandShuttleConsoleComponent>();
            while (consoleQuery.MoveNext(out _, out var console))
                console.RecyclingOutpostGrid = recyclingOutpostGridUid;
        }

        if (shuttleGridUid.HasValue &&
            TryComp(shuttleGridUid.Value, out ShuttleComponent? shuttleComp))
        {
            EntityUid? stationGrid = null;
            var stationQuery = EntityQueryEnumerator<StationDataComponent>();
            while (stationQuery.MoveNext(out var stationUid, out _))
            {
                stationGrid = _station.GetLargestGrid(stationUid);
                if (stationGrid != null)
                    break;
            }

            if (stationGrid != null)
                _shuttle.FTLToDock(shuttleGridUid.Value, shuttleComp, stationGrid.Value);
        }

        for (var i = 0; i < comp.NecroposisSpikeCount; i++)
        {
            var pos = FindSafeSpikePosition(placedBounds, comp, biome, mapUid, planetGrid);
            _maps.SetTile(mapUid, planetGrid, new Vector2i((int)pos.X, (int)pos.Y), basaltTile);
            Spawn(comp.NecroposisSpikePrototype, new EntityCoordinates(mapUid, pos + new Vector2(0.5f, 0.5f)));
        }

        foreach (var proto in comp.MegafaunaPrototypes)
        {
            var pos = FindSafeMobPosition(placedBounds, comp);
            _maps.SetTile(mapUid, planetGrid, new Vector2i((int)pos.X, (int)pos.Y), basaltTile);
            Spawn(proto, new EntityCoordinates(mapUid, pos + new Vector2(0.5f, 0.5f)));
        }

        SpawnBorderWall(mapUid, planetGrid, comp.BorderRadius);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandPlanetRuleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.PendingAtmosRebuild.Count == 0)
                continue;

            var done = new List<EntityUid>();
            foreach (var gridUid in comp.PendingAtmosRebuild)
            {
                if (!TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos) ||
                    !TryComp<MapGridComponent>(gridUid, out var gridMapComp))
                {
                    done.Add(gridUid);
                    continue;
                }

                if (gridAtmos.Tiles.Count == 0)
                    continue; // wait until atmos system initializes tiles

                _atmos.RebuildGridAtmosphere((gridUid, gridAtmos, gridMapComp));
                done.Add(gridUid);
            }

            foreach (var uid in done)
                comp.PendingAtmosRebuild.Remove(uid);
        }
    }

    private Vector2 FindSafePosition(Box2 localBounds, List<Box2> placedBounds, LavalandPlanetRuleComponent comp)
    {
        const int maxRetries = 50;
        const float padding = 5f;

        for (var i = 0; i < maxRetries; i++)
        {
            var pos = GetRandomPosition(comp.MinStructureDistance, comp.MaxStructureDistance);
            var worldBounds = localBounds.Translated(pos).Enlarged(padding);

            var overlaps = false;
            foreach (var existing in placedBounds)
            {
                if (existing.Intersects(worldBounds))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
                return pos;
        }

        return GetRandomPosition(comp.MinStructureDistance, comp.MaxStructureDistance);
    }

    private void SpawnBorderWall(EntityUid mapUid, MapGridComponent planetGrid, float radius)
    {
        var basaltTile = new Tile(_tileDefManager["FloorBasalt"].TileId);
        var r = (int)radius;
        var positions = new HashSet<Vector2i>();

        for (var y = -(r + 2); y <= r + 2; y++)
        {
            for (var x = -(r + 2); x <= r + 2; x++)
            {
                var dist = MathF.Sqrt(x * x + y * y);
                if (dist >= radius - 0.5f && dist <= radius + 1.5f)
                    positions.Add(new Vector2i(x, y));
            }
        }

        var tiles = new List<(Vector2i, Tile)>(positions.Count);
        foreach (var pos in positions)
            tiles.Add((pos, basaltTile));
        _maps.SetTiles(mapUid, planetGrid, tiles);

        foreach (var pos in positions)
            Spawn("WallRockBasalt", new EntityCoordinates(mapUid, new Vector2(pos.X + 0.5f, pos.Y + 0.5f)));
    }

    private Vector2 FindSafeMobPosition(List<Box2> placedBounds, LavalandPlanetRuleComponent comp)
    {
        const int maxRetries = 50;
        for (var i = 0; i < maxRetries; i++)
        {
            var pos = GetRandomPosition(comp.MinStructureDistance, comp.MaxStructureDistance);
            var inside = false;
            foreach (var bounds in placedBounds)
            {
                if (bounds.Contains(pos))
                {
                    inside = true;
                    break;
                }
            }
            if (!inside)
                return pos;
        }
        return GetRandomPosition(comp.MinStructureDistance, comp.MaxStructureDistance);
    }

    private Vector2 FindSafeSpikePosition(List<Box2> placedBounds, LavalandPlanetRuleComponent comp, BiomeComponent biome, EntityUid mapUid, MapGridComponent grid)
    {
        const int maxRetries = 100;
        for (var i = 0; i < maxRetries; i++)
        {
            var pos = GetRandomPosition(comp.MinStructureDistance, comp.MaxStructureDistance);
            var tileIndex = new Vector2i((int)pos.X, (int)pos.Y);

            var inside = false;
            foreach (var bounds in placedBounds)
            {
                if (bounds.Contains(pos))
                {
                    inside = true;
                    break;
                }
            }
            if (inside)
                continue;

            // Check center + 4 cardinal neighbors so the spike doesn't end up
            // on an open island surrounded by a basalt wall cluster.
            var blocked = false;
            Span<Vector2i> toCheck = stackalloc Vector2i[]
            {
                tileIndex,
                new Vector2i(tileIndex.X + 1, tileIndex.Y),
                new Vector2i(tileIndex.X - 1, tileIndex.Y),
                new Vector2i(tileIndex.X, tileIndex.Y + 1),
                new Vector2i(tileIndex.X, tileIndex.Y - 1),
            };
            foreach (var t in toCheck)
            {
                if (_biome.TryGetEntity(t, biome, (mapUid, grid), out var entity) && entity == "WallRockBasalt")
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked)
                continue;

            return pos;
        }
        return GetRandomPosition(comp.MinStructureDistance, comp.MaxStructureDistance);
    }

    private Vector2 GetRandomPosition(float minDist, float maxDist)
    {
        var angle = _random.NextFloat() * MathF.Tau;
        var dist = minDist + _random.NextFloat() * (maxDist - minDist);
        return new Vector2(MathF.Round(MathF.Cos(angle) * dist), MathF.Round(MathF.Sin(angle) * dist));
    }
}
