using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.ShipDrowning;

public sealed class SeaShipFloodOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> FloodShader = "SeaShipFlood";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private static readonly Vector2i[] CardinalOffsets =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0),
    };

    private const float TileQueryPadding = 3f;
    private const float SmallFloodMeshStep = 0.07f;
    private const float MediumFloodMeshStep = 0.085f;
    private const float LargeFloodMeshStep = 0.12f;
    private const float HugeFloodMeshStep = 0.16f;
    private const float MassiveFloodMeshStep = 0.2f;
    private const int MaxFloodDrawVerticesPerBatch = 12288;
    private const float MinMeshRebuildInterval = 0.05f;
    private const float CoverageRebuildThreshold = 0.008f;
    private const float WaterDriftRebuildThresholdSquared = 0.0004f;
    private const float FloodBlobReachScale = 1.55f;
    private const float FloodBlobBucketPadding = 0.25f;

    private HashSet<Vector2i> _occupiedTiles = new();
    private Dictionary<Vector2i, int> _boundaryDistance = new();
    private readonly Dictionary<Vector2i, float> _tileFill = new();
    private readonly Dictionary<Vector2i, float> _vertexFill = new();
    private readonly HashSet<Vector2i> _vertexCandidates = new();
    private readonly Queue<Vector2i> _distanceQueue = new();
    private readonly List<FloodBlob> _floodBlobs = new();
    private readonly Dictionary<Vector2i, List<int>> _floodBlobBuckets = new();
    private List<DrawVertexUV2D> _meshVertices = new();
    private readonly ShaderInstance _baseFloodShader;
    private readonly List<ShaderInstance> _floodShaders = new();
    private readonly Texture _whiteTexture;
    private readonly Dictionary<EntityUid, FloodGridCache> _gridCaches = new();
    private int _activeFloodShaderCount;
    private float[] _sampleBuffer = Array.Empty<float>();

    private readonly record struct FloodBlob(Vector2 Center, Vector2 Radius, float Strength);

    private sealed class FloodGridCache
    {
        public readonly HashSet<Vector2i> OccupiedTiles = new();
        public readonly Dictionary<Vector2i, int> BoundaryDistance = new();
        public readonly List<DrawVertexUV2D> MeshVertices = new();
        public int ShapeVersion = -1;
        public Vector2i MinTile;
        public Vector2i MaxTile;
        public int MaxBoundaryDistance = -1;
        public float CachedDrownRatio = -1f;
        public Vector2 CachedWaterDrift;
        public TimeSpan LastMeshRebuildTime = TimeSpan.MinValue;
        public bool MeshValid;
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public SeaShipFloodOverlay()
    {
        IoCManager.InjectDependencies(this);
        _baseFloodShader = _prototypeManager.Index(FloodShader).Instance().Duplicate();
        _whiteTexture = _resourceCache.GetFallback<TextureResource>().Texture;
        ZIndex = 14;
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        _baseFloodShader.Dispose();

        foreach (var shader in _floodShaders)
        {
            shader.Dispose();
        }

        _floodShaders.Clear();
        _gridCaches.Clear();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) && !sea.Disabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var xformSystem = _entityManager.System<SharedTransformSystem>();
        var mapSystem = _entityManager.System<SharedMapSystem>();
        var visibleBounds = args.WorldAABB.Enlarged(TileQueryPadding);
        var query = _entityManager.AllEntityQueryEnumerator<MapGridComponent, TransformComponent, ShipDrowningComponent>();
        _activeFloodShaderCount = 0;

        while (query.MoveNext(out var uid, out var grid, out var xform, out var drowning))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (drowning.DrownLevel <= 0 || drowning.DrownMaxLevel <= 0)
                continue;

            var drownLevel = drowning.VisualDataInitialized ? drowning.VisualDrownLevel : drowning.DrownLevel;
            var drownRatio = drownLevel / drowning.DrownMaxLevel;
            if (drownRatio <= 0.004f)
                continue;

            var worldMatrix = xformSystem.GetWorldMatrix(xform);
            var worldBounds = worldMatrix.TransformBox(grid.LocalAABB);

            if (!worldBounds.Intersects(visibleBounds))
                continue;

            DrawFlood(args.WorldHandle, uid, grid, worldMatrix, drownRatio, drowning.VisualWaterOffset, mapSystem);
        }
    }

    private void DrawFlood(
        DrawingHandleWorld handle,
        EntityUid gridUid,
        MapGridComponent grid,
        Matrix3x2 worldMatrix,
        float drownRatio,
        Vector2 waterDrift,
        SharedMapSystem mapSystem)
    {
        if (!EnsureGridCache(gridUid, grid, mapSystem, out var cache))
            return;

        var shipSeed = gridUid.Id;

        _occupiedTiles = cache.OccupiedTiles;
        _boundaryDistance = cache.BoundaryDistance;
        _meshVertices = cache.MeshVertices;

        var clampedDrownRatio = Math.Clamp(drownRatio, 0f, 2.4f);
        var coverageRatio = Math.Clamp(clampedDrownRatio, 0f, 1f);
        var overflowRatio = Math.Clamp(clampedDrownRatio - 1f, 0f, 1.4f);
        if (NeedsFloodMeshRebuild(cache, clampedDrownRatio, waterDrift))
            RebuildFloodMesh(cache, shipSeed, clampedDrownRatio, waterDrift);

        if (cache.MeshVertices.Count == 0)
            return;

        var shader = GetFloodShader();
        shader.SetParameter("shipSeed", (float) shipSeed);
        shader.SetParameter("coverageRatio", coverageRatio);
        shader.SetParameter("overflowRatio", overflowRatio);
        shader.SetParameter("driftX", waterDrift.X);
        shader.SetParameter("driftY", waterDrift.Y);

        handle.UseShader(shader);
        handle.SetTransform(worldMatrix);
        var meshSpan = CollectionsMarshal.AsSpan(cache.MeshVertices);
        for (var start = 0; start < meshSpan.Length; start += MaxFloodDrawVerticesPerBatch)
        {
            var count = Math.Min(MaxFloodDrawVerticesPerBatch, meshSpan.Length - start);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _whiteTexture, meshSpan.Slice(start, count));
        }
        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void BuildFloodMesh(
        float minX,
        float minY,
        float maxX,
        float maxY,
        float coverageRatio,
        float overflowRatio,
        float globalEdgeFill,
        int shipSeed,
        Vector2 waterDrift,
        float meshStep)
    {
        var fullFlood = coverageRatio >= 0.95f || overflowRatio >= 0.03f;
        var threshold = fullFlood
            ? 0.02f
            : MathHelper.Lerp(0.22f, 0.145f, MathF.Pow(Math.Clamp(coverageRatio, 0f, 1f), 0.78f));
        var cellsX = (int) MathF.Ceiling((maxX - minX) / meshStep);
        var cellsY = (int) MathF.Ceiling((maxY - minY) / meshStep);
        var sampleWidth = cellsX + 1;
        var sampleHeight = cellsY + 1;
        var sampleCount = sampleWidth * sampleHeight;
        var samples = EnsureSampleBuffer(sampleCount);

        for (var y = 0; y < sampleHeight; y++)
        {
            var sampleY = y == cellsY ? maxY - 0.0001f : minY + y * meshStep;

            for (var x = 0; x < sampleWidth; x++)
            {
                var sampleX = x == cellsX ? maxX - 0.0001f : minX + x * meshStep;
                samples[x + y * sampleWidth] = SampleFloodField(
                    new Vector2(sampleX, sampleY),
                    coverageRatio,
                    overflowRatio,
                    globalEdgeFill,
                    shipSeed,
                    waterDrift);
            }
        }

        for (var y = 0; y < cellsY; y++)
        {
            var y0 = minY + y * meshStep;
            var y1 = y == cellsY - 1 ? maxY : minY + (y + 1) * meshStep;

            for (var x = 0; x < cellsX; x++)
            {
                var x0 = minX + x * meshStep;
                var x1 = x == cellsX - 1 ? maxX : minX + (x + 1) * meshStep;

                var p00 = new Vector2(x0, y0);
                var p10 = new Vector2(x1, y0);
                var p11 = new Vector2(x1, y1);
                var p01 = new Vector2(x0, y1);

                var i00 = x + y * sampleWidth;
                var i10 = i00 + 1;
                var i01 = x + (y + 1) * sampleWidth;
                var i11 = i01 + 1;

                var v00 = samples[i00];
                var v10 = samples[i10];
                var v11 = samples[i11];
                var v01 = samples[i01];

                AppendIsoTriangle(p00, p10, p11, v00, v10, v11, threshold);
                AppendIsoTriangle(p00, p11, p01, v00, v11, v01, threshold);
            }
        }
    }

    private void AppendIsoTriangle(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        float v0,
        float v1,
        float v2,
        float threshold)
    {
        var inside0 = v0 >= threshold;
        var inside1 = v1 >= threshold;
        var inside2 = v2 >= threshold;
        var insideCount = (inside0 ? 1 : 0) + (inside1 ? 1 : 0) + (inside2 ? 1 : 0);
        if (insideCount == 0)
            return;

        if (insideCount == 3)
        {
            AppendFloodTriangle(p0, p1, p2);
            return;
        }

        if (insideCount == 1)
        {
            if (inside0)
            {
                var i01 = IntersectEdge(p0, p1, v0, v1, threshold);
                var i20 = IntersectEdge(p2, p0, v2, v0, threshold);
                AppendFloodTriangle(p0, i01, i20);
                return;
            }

            if (inside1)
            {
                var i01 = IntersectEdge(p0, p1, v0, v1, threshold);
                var i12 = IntersectEdge(p1, p2, v1, v2, threshold);
                AppendFloodTriangle(i01, p1, i12);
                return;
            }

            var i12Only = IntersectEdge(p1, p2, v1, v2, threshold);
            var i20Only = IntersectEdge(p2, p0, v2, v0, threshold);
            AppendFloodTriangle(i12Only, p2, i20Only);
            return;
        }

        if (!inside0)
        {
            var i01 = IntersectEdge(p0, p1, v0, v1, threshold);
            var i20 = IntersectEdge(p2, p0, v2, v0, threshold);
            AppendFloodTriangle(i01, p1, p2);
            AppendFloodTriangle(i01, p2, i20);
            return;
        }

        if (!inside1)
        {
            var i01 = IntersectEdge(p0, p1, v0, v1, threshold);
            var i12 = IntersectEdge(p1, p2, v1, v2, threshold);
            AppendFloodTriangle(p0, i01, i12);
            AppendFloodTriangle(p0, i12, p2);
            return;
        }

        var i12Last = IntersectEdge(p1, p2, v1, v2, threshold);
        var i20Last = IntersectEdge(p2, p0, v2, v0, threshold);
        AppendFloodTriangle(p0, p1, i12Last);
        AppendFloodTriangle(p0, i12Last, i20Last);
    }

    private void AppendFloodTriangle(Vector2 a, Vector2 b, Vector2 c)
    {
        _meshVertices.Add(new DrawVertexUV2D(a, a));
        _meshVertices.Add(new DrawVertexUV2D(b, b));
        _meshVertices.Add(new DrawVertexUV2D(c, c));
    }

    private static Vector2 IntersectEdge(Vector2 a, Vector2 b, float va, float vb, float threshold)
    {
        if (MathF.Abs(vb - va) <= 0.0001f)
            return (a + b) * 0.5f;

        var t = Math.Clamp((threshold - va) / (vb - va), 0f, 1f);
        return Vector2.Lerp(a, b, t);
    }

    private float SampleFloodField(
        Vector2 position,
        float coverageRatio,
        float overflowRatio,
        float globalEdgeFill,
        int shipSeed,
        Vector2 waterDrift)
    {
        var tile = new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
        if (!_occupiedTiles.Contains(tile))
            return 0f;

        if (coverageRatio >= 0.95f || overflowRatio >= 0.03f)
            return 1.4f;

        var uv = position - new Vector2(tile.X, tile.Y);
        var highFloodEdgeLock = SmoothStep(0.82f, 0.95f, coverageRatio);
        var driftScale = MathHelper.Lerp(1f, 0.18f, highFloodEdgeLock);
        var shiftedPosition = position - waterDrift * driftScale;
        var field = 0f;

        var blobBucket = new Vector2i((int) MathF.Floor(shiftedPosition.X), (int) MathF.Floor(shiftedPosition.Y));
        if (_floodBlobBuckets.TryGetValue(blobBucket, out var blobIndices))
        {
            foreach (var blobIndex in blobIndices)
            {
                var blob = _floodBlobs[blobIndex];
                var delta = shiftedPosition - blob.Center;
                var reach = blob.Radius * FloodBlobReachScale;

                if (MathF.Abs(delta.X) > reach.X || MathF.Abs(delta.Y) > reach.Y)
                    continue;

                field += EvaluateFloodBlob(delta, blob.Radius, blob.Strength);
            }
        }

        field = 1f - MathF.Exp(-field * 1.06f);

        var boundaryBand = 0f;

        if (!_occupiedTiles.Contains(tile + new Vector2i(0, 1)))
            boundaryBand = MathF.Max(boundaryBand, SmoothStep(0.7f, 1f, uv.Y));

        if (!_occupiedTiles.Contains(tile + new Vector2i(1, 0)))
            boundaryBand = MathF.Max(boundaryBand, SmoothStep(0.7f, 1f, uv.X));

        if (!_occupiedTiles.Contains(tile + new Vector2i(0, -1)))
            boundaryBand = MathF.Max(boundaryBand, SmoothStep(0.3f, 0f, uv.Y));

        if (!_occupiedTiles.Contains(tile + new Vector2i(-1, 0)))
            boundaryBand = MathF.Max(boundaryBand, SmoothStep(0.3f, 0f, uv.X));

        if (boundaryBand > 0f)
        {
            var edgeFloor = MathHelper.Lerp(
                MathF.Max(globalEdgeFill, 0.12f + coverageRatio * 0.1f),
                1.12f,
                highFloodEdgeLock);
            field = MathF.Max(field, boundaryBand * edgeFloor);
        }

        var staticNoise = ContinuousFloodNoise(shiftedPosition, shipSeed);
        field += staticNoise * (0.007f + field * 0.016f);

        return Math.Clamp(field, 0f, 1.5f);
    }

    private void BuildFloodBlobs(float coverageRatio, int shipSeed)
    {
        var growth = MathHelper.Lerp(0.28f, 1.44f, MathF.Pow(Math.Clamp(coverageRatio, 0f, 1f), 0.72f));
        var microGrowth = MathHelper.Lerp(0.24f, 0.74f, MathF.Pow(Math.Clamp(coverageRatio, 0f, 1f), 0.62f));
        var smallPuddleFactor = SmoothStep(0.035f, 0.11f, coverageRatio);
        var edgePresence = SmoothStep(0.04f, 0.16f, coverageRatio);
        var minBounds = new Vector2(float.MaxValue, float.MaxValue);
        var maxBounds = new Vector2(float.MinValue, float.MinValue);

        foreach (var tile in _occupiedTiles)
        {
            minBounds.X = MathF.Min(minBounds.X, tile.X);
            minBounds.Y = MathF.Min(minBounds.Y, tile.Y);
            maxBounds.X = MathF.Max(maxBounds.X, tile.X + 1f);
            maxBounds.Y = MathF.Max(maxBounds.Y, tile.Y + 1f);
        }

        foreach (var tile in _occupiedTiles)
        {
            var fill = GetTileFill(tile);
            var boundaryTile = IsBoundaryTile(tile);
            var edgeAnchorFill = boundaryTile
                ? 0.032f + edgePresence * 0.18f + coverageRatio * 0.035f
                : 0f;
            var tilePosition = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
            var tileNoise = 0.5f + 0.5f * ContinuousFloodNoise(tilePosition * 0.63f + new Vector2(1.7f, -0.8f), shipSeed + 53);
            var clusterBias = SmoothStep(0.34f, 0.88f, tileNoise + coverageRatio * 0.18f);

            if (fill > 0.001f && clusterBias > 0.08f)
            {
                var blobFill = MathF.Max(fill * MathHelper.Lerp(0.18f, 1.12f, clusterBias), fill * 0.16f);
                // TODO: придумать что-то лучше подобных сидов
                var seedA = Hash(tile.X * 71237 + tile.Y * 51283 + shipSeed * 17);
                var seedB = Hash(tile.X * 32531 + tile.Y * 91841 + shipSeed * 31);
                var seedC = Hash(tile.X * 48281 + tile.Y * 18131 + shipSeed * 47);
                var seedD = Hash(tile.X * 87323 + tile.Y * 27191 + shipSeed * 59);
                var seedE = Hash(tile.X * 11897 + tile.Y * 66221 + shipSeed * 71);
                var shiftScale = MathHelper.Lerp(0.24f, 0.065f, blobFill) * (0.86f + (1f - clusterBias) * 0.18f);
                var center = new Vector2(
                    tile.X + 0.5f + (seedA - 0.5f) * shiftScale * 2f,
                    tile.Y + 0.5f + (seedB - 0.5f) * shiftScale * 2f);
                var baseRadius = MathHelper.Lerp(0.058f, 0.34f, MathF.Pow(blobFill, 0.72f)) * growth;
                var radius = new Vector2(
                    baseRadius * (0.72f + (seedC - 0.5f) * 0.62f),
                    baseRadius * (0.64f + (seedD - 0.5f) * 0.5f));
                AddFloodBlob(center, radius, 0.14f + blobFill * 0.58f + clusterBias * 0.08f + smallPuddleFactor * 0.03f);

                if (blobFill > 0.11f || smallPuddleFactor > 0.001f && clusterBias > 0.22f)
                {
                    var seedF = Hash(tile.X * 99431 + tile.Y * 7717 + shipSeed * 89);
                    var seedG = Hash(tile.X * 27509 + tile.Y * 18439 + shipSeed * 97);
                    var childCenter = center + new Vector2(
                        (seedF - 0.5f) * radius.X * 0.58f,
                        (seedG - 0.5f) * radius.Y * 0.58f);
                    AddFloodBlob(
                        childCenter,
                        radius * (0.46f + seedE * 0.24f + clusterBias * 0.18f + smallPuddleFactor * 0.12f),
                        0.09f + blobFill * 0.2f + clusterBias * 0.06f + smallPuddleFactor * 0.05f);

                    var seedH = Hash(tile.X * 44741 + tile.Y * 53323 + shipSeed * 131);
                    var seedI = Hash(tile.X * 73121 + tile.Y * 14347 + shipSeed * 149);
                    var microCenter = center + new Vector2(
                        (seedH - 0.5f) * 0.68f,
                        (seedI - 0.5f) * 0.68f);
                    var microRadius = new Vector2(
                        (0.035f + blobFill * 0.06f) * microGrowth * (0.9f + seedI * 0.24f),
                        (0.028f + blobFill * 0.055f) * microGrowth * (0.85f + seedH * 0.22f));
                    AddFloodBlob(microCenter, microRadius, 0.045f + blobFill * 0.08f + clusterBias * 0.03f);
                }
            }

            var east = tile + new Vector2i(1, 0);
            if (_occupiedTiles.Contains(east))
            {
                var eastFill = GetTileFill(east);
                if (eastFill > 0.01f)
                {
                    var edgeFill = (fill + eastFill) * 0.5f;
                    var edgeSeed = Hash(tile.X * 61291 + tile.Y * 49157 + shipSeed * 101);
                    AddFloodBlob(
                        new Vector2(tile.X + 1f, tile.Y + 0.5f + (edgeSeed - 0.5f) * 0.18f),
                        new Vector2(
                            (0.08f + edgeFill * 0.12f) * growth,
                            (0.16f + edgeFill * 0.16f) * growth),
                        0.1f + edgeFill * 0.28f + smallPuddleFactor * 0.04f);
                }
            }
            else
            {
                var edgeNoise = 0.5f + 0.5f * ContinuousFloodNoise(new Vector2(tile.X + 1f, tile.Y + 0.5f) * 0.58f, shipSeed + 211);
                var outerEdgeFill = MathF.Max(
                    fill * MathHelper.Lerp(0.34f, 0.92f, edgeNoise),
                    0.03f + edgePresence * (0.16f + edgeNoise * 0.28f) + smallPuddleFactor * (0.08f + edgeNoise * 0.08f));
                var edgeGate = SmoothStep(0.34f, 0.83f, edgeNoise + outerEdgeFill * 0.42f);
                if (edgeGate > 0.06f)
                {
                    var edgeSeed = Hash(tile.X * 24179 + tile.Y * 68291 + shipSeed * 167);
                    var edgeSeedB = Hash(tile.X * 39119 + tile.Y * 81799 + shipSeed * 283);
                    var edgeSeedC = Hash(tile.X * 73303 + tile.Y * 19139 + shipSeed * 307);
                    var mainCenter = new Vector2(
                        tile.X + 1.05f + (edgeSeedB - 0.5f) * 0.03f,
                        tile.Y + 0.5f + (edgeSeed - 0.5f) * (0.18f + edgeNoise * 0.42f));
                    var mainRadius = new Vector2(
                        (0.09f + outerEdgeFill * (0.13f + edgeNoise * 0.08f)) * growth,
                        (0.14f + outerEdgeFill * (0.15f + edgeNoise * 0.15f)) * growth);
                    AddFloodBlob(
                        mainCenter,
                        mainRadius,
                        (0.1f + outerEdgeFill * 0.28f + edgeAnchorFill * 0.18f) * edgeGate);

                    if (edgeSeedC > 0.48f)
                    {
                        var followCenter = mainCenter + new Vector2(
                            (edgeSeedC - 0.5f) * 0.04f,
                            (edgeSeedB - 0.5f) * (0.18f + edgeNoise * 0.36f));
                        var followRadius = new Vector2(
                            mainRadius.X * (0.66f + edgeSeed * 0.22f),
                            mainRadius.Y * (0.5f + edgeSeedC * 0.28f));
                        AddFloodBlob(
                            followCenter,
                            followRadius,
                            (0.055f + outerEdgeFill * 0.18f) * edgeGate);
                    }
                }
            }

            var north = tile + new Vector2i(0, 1);
            if (_occupiedTiles.Contains(north))
            {
                var northFill = GetTileFill(north);
                if (northFill > 0.01f)
                {
                    var edgeFill = (fill + northFill) * 0.5f;
                    var edgeSeed = Hash(tile.X * 21661 + tile.Y * 90787 + shipSeed * 107);
                    AddFloodBlob(
                        new Vector2(tile.X + 0.5f + (edgeSeed - 0.5f) * 0.18f, tile.Y + 1f),
                        new Vector2(
                            (0.16f + edgeFill * 0.16f) * growth,
                            (0.08f + edgeFill * 0.12f) * growth),
                        0.1f + edgeFill * 0.28f + smallPuddleFactor * 0.04f);
                }
            }
            else
            {
                var edgeNoise = 0.5f + 0.5f * ContinuousFloodNoise(new Vector2(tile.X + 0.5f, tile.Y + 1f) * 0.58f, shipSeed + 223);
                var outerEdgeFill = MathF.Max(
                    fill * MathHelper.Lerp(0.34f, 0.92f, edgeNoise),
                    0.03f + edgePresence * (0.16f + edgeNoise * 0.28f) + smallPuddleFactor * (0.08f + edgeNoise * 0.08f));
                var edgeGate = SmoothStep(0.34f, 0.83f, edgeNoise + outerEdgeFill * 0.42f);
                if (edgeGate > 0.06f)
                {
                    var edgeSeed = Hash(tile.X * 51787 + tile.Y * 17623 + shipSeed * 173);
                    var edgeSeedB = Hash(tile.X * 27803 + tile.Y * 69217 + shipSeed * 293);
                    var edgeSeedC = Hash(tile.X * 64109 + tile.Y * 35897 + shipSeed * 311);
                    var mainCenter = new Vector2(
                        tile.X + 0.5f + (edgeSeed - 0.5f) * (0.18f + edgeNoise * 0.42f),
                        tile.Y + 1.05f + (edgeSeedB - 0.5f) * 0.03f);
                    var mainRadius = new Vector2(
                        (0.14f + outerEdgeFill * (0.15f + edgeNoise * 0.15f)) * growth,
                        (0.09f + outerEdgeFill * (0.13f + edgeNoise * 0.08f)) * growth);
                    AddFloodBlob(
                        mainCenter,
                        mainRadius,
                        (0.1f + outerEdgeFill * 0.28f + edgeAnchorFill * 0.18f) * edgeGate);

                    if (edgeSeedC > 0.48f)
                    {
                        var followCenter = mainCenter + new Vector2(
                            (edgeSeedB - 0.5f) * (0.18f + edgeNoise * 0.36f),
                            (edgeSeedC - 0.5f) * 0.04f);
                        var followRadius = new Vector2(
                            mainRadius.X * (0.5f + edgeSeedC * 0.28f),
                            mainRadius.Y * (0.66f + edgeSeed * 0.22f));
                        AddFloodBlob(
                            followCenter,
                            followRadius,
                            (0.055f + outerEdgeFill * 0.18f) * edgeGate);
                    }
                }
            }

            var west = tile + new Vector2i(-1, 0);
            if (!_occupiedTiles.Contains(west))
            {
                var edgeNoise = 0.5f + 0.5f * ContinuousFloodNoise(new Vector2(tile.X, tile.Y + 0.5f) * 0.58f, shipSeed + 227);
                var outerEdgeFill = MathF.Max(
                    fill * MathHelper.Lerp(0.34f, 0.92f, edgeNoise),
                    0.03f + edgePresence * (0.16f + edgeNoise * 0.28f) + smallPuddleFactor * (0.08f + edgeNoise * 0.08f));
                var edgeGate = SmoothStep(0.34f, 0.83f, edgeNoise + outerEdgeFill * 0.42f);
                if (edgeGate > 0.06f)
                {
                    var edgeSeed = Hash(tile.X * 82963 + tile.Y * 29269 + shipSeed * 179);
                    var edgeSeedB = Hash(tile.X * 55927 + tile.Y * 48313 + shipSeed * 317);
                    var edgeSeedC = Hash(tile.X * 13297 + tile.Y * 71867 + shipSeed * 331);
                    var mainCenter = new Vector2(
                        tile.X - 0.05f + (edgeSeedB - 0.5f) * 0.03f,
                        tile.Y + 0.5f + (edgeSeed - 0.5f) * (0.18f + edgeNoise * 0.42f));
                    var mainRadius = new Vector2(
                        (0.09f + outerEdgeFill * (0.13f + edgeNoise * 0.08f)) * growth,
                        (0.14f + outerEdgeFill * (0.15f + edgeNoise * 0.15f)) * growth);
                    AddFloodBlob(
                        mainCenter,
                        mainRadius,
                        (0.1f + outerEdgeFill * 0.28f + edgeAnchorFill * 0.18f) * edgeGate);

                    if (edgeSeedC > 0.48f)
                    {
                        var followCenter = mainCenter + new Vector2(
                            (edgeSeedC - 0.5f) * 0.04f,
                            (edgeSeedB - 0.5f) * (0.18f + edgeNoise * 0.36f));
                        var followRadius = new Vector2(
                            mainRadius.X * (0.66f + edgeSeed * 0.22f),
                            mainRadius.Y * (0.5f + edgeSeedC * 0.28f));
                        AddFloodBlob(
                            followCenter,
                            followRadius,
                            (0.055f + outerEdgeFill * 0.18f) * edgeGate);
                    }
                }
            }

            var south = tile + new Vector2i(0, -1);
            if (!_occupiedTiles.Contains(south))
            {
                var edgeNoise = 0.5f +
                                0.5f * ContinuousFloodNoise(new Vector2(tile.X + 0.5f, tile.Y) * 0.58f, shipSeed + 229);
                var outerEdgeFill = MathF.Max(
                    fill * MathHelper.Lerp(0.34f, 0.92f, edgeNoise),
                    0.03f + edgePresence * (0.16f + edgeNoise * 0.28f) +
                    smallPuddleFactor * (0.08f + edgeNoise * 0.08f));
                var edgeGate = SmoothStep(0.34f, 0.83f, edgeNoise + outerEdgeFill * 0.42f);
                if (!(edgeGate > 0.06f))
                    continue;

                var edgeSeed = Hash(tile.X * 18493 + tile.Y * 93763 + shipSeed * 181);
                var edgeSeedB = Hash(tile.X * 49223 + tile.Y * 36671 + shipSeed * 337);
                var edgeSeedC = Hash(tile.X * 71741 + tile.Y * 11527 + shipSeed * 347);
                var mainCenter = new Vector2(
                    tile.X + 0.5f + (edgeSeed - 0.5f) * (0.18f + edgeNoise * 0.42f),
                    tile.Y - 0.05f + (edgeSeedB - 0.5f) * 0.03f);
                var mainRadius = new Vector2(
                    (0.14f + outerEdgeFill * (0.15f + edgeNoise * 0.15f)) * growth,
                    (0.09f + outerEdgeFill * (0.13f + edgeNoise * 0.08f)) * growth);
                AddFloodBlob(
                    mainCenter,
                    mainRadius,
                    (0.1f + outerEdgeFill * 0.28f + edgeAnchorFill * 0.18f) * edgeGate);

                if (!(edgeSeedC > 0.48f))
                    continue;
                var followCenter = mainCenter + new Vector2(
                    (edgeSeedB - 0.5f) * (0.18f + edgeNoise * 0.36f),
                    (edgeSeedC - 0.5f) * 0.04f);
                var followRadius = new Vector2(
                    mainRadius.X * (0.5f + edgeSeedC * 0.28f),
                    mainRadius.Y * (0.66f + edgeSeed * 0.22f));
                AddFloodBlob(
                    followCenter,
                    followRadius,
                    (0.055f + outerEdgeFill * 0.18f) * edgeGate);
            }
        }

        AddShipFloodClusters(coverageRatio, shipSeed, growth, smallPuddleFactor, minBounds, maxBounds);

        foreach (var (vertex, fill) in _vertexFill)
        {
            if (fill <= 0.015f)
                continue;

            var seedA = Hash(vertex.X * 43133 + vertex.Y * 17183 + shipSeed * 113);
            var seedB = Hash(vertex.X * 68261 + vertex.Y * 38723 + shipSeed * 127);
            var center = new Vector2(
                vertex.X + (seedA - 0.5f) * 0.12f,
                vertex.Y + (seedB - 0.5f) * 0.12f);
            var radius = Vector2.One * ((0.05f + fill * 0.1f) * growth);
            AddFloodBlob(center, radius, 0.08f + fill * 0.18f + smallPuddleFactor * 0.04f);
        }
    }

    private void AddShipFloodClusters(
        float coverageRatio,
        int shipSeed,
        float growth,
        float smallPuddleFactor,
        Vector2 minBounds,
        Vector2 maxBounds)
    {
        var clusterPresence = SmoothStep(0.05f, 0.26f, coverageRatio);
        if (clusterPresence <= 0.001f)
            return;

        var clusterCount = Math.Clamp((int) MathF.Round(MathHelper.Lerp(2f, 5f, MathF.Pow(clusterPresence, 0.72f))), 2, 5);

        for (var clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
        {
            if (!TryFindShipClusterAnchor(shipSeed, clusterIndex, minBounds, maxBounds, out var anchor))
                continue;

            var clusterSeedA = Hash(shipSeed * 391 + clusterIndex * 211);
            var clusterSeedB = Hash(shipSeed * 613 + clusterIndex * 127);
            var clusterSeedC = Hash(shipSeed * 877 + clusterIndex * 163);
            var coreRadiusBase = (0.12f + coverageRatio * 0.17f + clusterSeedB * 0.06f) * growth;
            var coreRadius = new Vector2(
                coreRadiusBase * (0.88f + clusterSeedA * 0.46f),
                coreRadiusBase * (0.8f + clusterSeedC * 0.42f));

            AddFloodBlob(
                anchor,
                coreRadius,
                0.25f + coverageRatio * 0.32f + smallPuddleFactor * 0.08f);

            var lobeCount = 5 + (int) MathF.Round(clusterSeedB * 3f + coverageRatio * 2f);
            var baseAngle = clusterSeedA * MathF.Tau;
            var spread = (0.11f + coverageRatio * 0.08f + clusterSeedC * 0.035f) * growth;

            for (var lobeIndex = 0; lobeIndex < lobeCount; lobeIndex++)
            {
                var lobeSeedA = Hash(shipSeed * 971 + clusterIndex * 101 + lobeIndex * 37);
                var lobeSeedB = Hash(shipSeed * 557 + clusterIndex * 149 + lobeIndex * 73);
                var lobeSeedC = Hash(shipSeed * 419 + clusterIndex * 181 + lobeIndex * 97);
                var angle = baseAngle +
                    lobeIndex * (MathF.Tau / MathF.Max(lobeCount, 1)) +
                    (lobeSeedA - 0.5f) * 0.9f;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var tangent = new Vector2(-dir.Y, dir.X);
                var distance = spread * (0.4f + lobeSeedB * 0.56f);
                var lobeCenter = anchor +
                    dir * distance +
                    tangent * spread * (lobeSeedC - 0.5f) * 0.38f;

                if (!TryMovePointInsideShip(anchor, ref lobeCenter))
                    continue;

                var lobeRadiusBase = coreRadiusBase * (0.52f + lobeSeedA * 0.42f);
                var lobeRadius = new Vector2(
                    lobeRadiusBase * (0.78f + lobeSeedB * 0.38f),
                    lobeRadiusBase * (0.66f + lobeSeedC * 0.34f));
                AddFloodBlob(
                    lobeCenter,
                    lobeRadius,
                    0.13f + coverageRatio * 0.18f + lobeSeedA * 0.06f);

                if (!(lobeSeedB > 0.34f))
                    continue;
                var microCenter = lobeCenter +
                                  tangent * lobeRadiusBase * (lobeSeedC - 0.5f) * 0.9f;

                if (!TryMovePointInsideShip(anchor, ref microCenter))
                    continue;

                var microRadius = new Vector2(
                    lobeRadiusBase * (0.28f + lobeSeedB * 0.18f),
                    lobeRadiusBase * (0.22f + lobeSeedA * 0.14f));
                AddFloodBlob(
                    microCenter,
                    microRadius,
                    0.06f + coverageRatio * 0.1f + smallPuddleFactor * 0.04f);
            }
        }
    }

    private bool TryFindShipClusterAnchor(
        int shipSeed,
        int clusterIndex,
        Vector2 minBounds,
        Vector2 maxBounds,
        out Vector2 anchor)
    {
        for (var attempt = 0; attempt < 18; attempt++)
        {
            var seedX = Hash(shipSeed * 313 + clusterIndex * 137 + attempt * 29);
            var seedY = Hash(shipSeed * 571 + clusterIndex * 173 + attempt * 43);
            var candidate = new Vector2(
                MathHelper.Lerp(minBounds.X + 0.18f, maxBounds.X - 0.18f, seedX),
                MathHelper.Lerp(minBounds.Y + 0.18f, maxBounds.Y - 0.18f, seedY));

            if (!IsPointInsideShip(candidate))
                continue;
            anchor = candidate;
            return true;
        }

        anchor = Vector2.Zero;
        return false;
    }

    private bool TryMovePointInsideShip(Vector2 fallback, ref Vector2 point)
    {
        if (IsPointInsideShip(point))
            return true;

        for (var i = 0; i < 4; i++)
        {
            point = Vector2.Lerp(point, fallback, 0.38f + i * 0.12f);
            if (IsPointInsideShip(point))
                return true;
        }

        return false;
    }

    private bool IsPointInsideShip(Vector2 point)
    {
        var tile = new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y));
        return _occupiedTiles.Contains(tile);
    }

    private void AddFloodBlob(Vector2 center, Vector2 radius, float strength)
    {
        if (radius.X <= 0.01f || radius.Y <= 0.01f || strength <= 0.001f)
            return;

        var blobIndex = _floodBlobs.Count;
        _floodBlobs.Add(new FloodBlob(center, radius, strength));

        var reach = radius * FloodBlobReachScale + new Vector2(FloodBlobBucketPadding, FloodBlobBucketPadding);
        var minTile = new Vector2i(
            (int) MathF.Floor(center.X - reach.X),
            (int) MathF.Floor(center.Y - reach.Y));
        var maxTile = new Vector2i(
            (int) MathF.Floor(center.X + reach.X),
            (int) MathF.Floor(center.Y + reach.Y));

        for (var x = minTile.X; x <= maxTile.X; x++)
        {
            for (var y = minTile.Y; y <= maxTile.Y; y++)
            {
                var tile = new Vector2i(x, y);
                if (!_floodBlobBuckets.TryGetValue(tile, out var bucket))
                {
                    bucket = new List<int>();
                    _floodBlobBuckets.Add(tile, bucket);
                }

                bucket.Add(blobIndex);
            }
        }
    }

    private static float EvaluateFloodBlob(Vector2 delta, Vector2 radius, float strength)
    {
        var normalized = new Vector2(
            delta.X / MathF.Max(radius.X, 0.001f),
            delta.Y / MathF.Max(radius.Y, 0.001f));
        var dist = normalized.X * normalized.X + normalized.Y * normalized.Y;
        var body = MathF.Max(0f, 1.08f - dist);
        body /= 1.08f;
        body = body * body * (3f - 2f * body);
        return body * strength;
    }

    private static float EdgeLink(float a, float b)
    {
        return Math.Clamp(MathF.Max(MathF.Min(a, b), (a + b) * 0.38f - 0.14f), 0f, 1.1f);
    }

    private void BuildVertexFills()
    {
        _vertexCandidates.Clear();

        foreach (var tile in _occupiedTiles)
        {
            _vertexCandidates.Add(tile);
            _vertexCandidates.Add(tile + new Vector2i(1, 0));
            _vertexCandidates.Add(tile + new Vector2i(0, 1));
            _vertexCandidates.Add(tile + new Vector2i(1, 1));
        }

        foreach (var vertex in _vertexCandidates)
        {
            var sum = 0f;
            var max = 0f;
            var count = 0;

            AccumulateVertexTileFill(vertex + new Vector2i(-1, -1), ref sum, ref max, ref count);
            AccumulateVertexTileFill(vertex + new Vector2i(0, -1), ref sum, ref max, ref count);
            AccumulateVertexTileFill(vertex + new Vector2i(-1, 0), ref sum, ref max, ref count);
            AccumulateVertexTileFill(vertex, ref sum, ref max, ref count);

            if (count == 0)
                continue;

            var average = sum / count;
            _vertexFill[vertex] = Math.Clamp(average * 0.76f + max * 0.12f, 0f, 1.08f);
        }
    }

    private int BuildBoundaryDistances()
    {
        var maxDistance = -1;

        foreach (var tile in _occupiedTiles)
        {
            if (!IsBoundaryTile(tile))
                continue;

            _boundaryDistance[tile] = 0;
            _distanceQueue.Enqueue(tile);
            maxDistance = 0;
        }

        while (_distanceQueue.TryDequeue(out var tile))
        {
            var nextDistance = _boundaryDistance[tile] + 1;

            foreach (var offset in CardinalOffsets)
            {
                var neighbor = tile + offset;
                if (!_occupiedTiles.Contains(neighbor) || _boundaryDistance.ContainsKey(neighbor))
                    continue;

                _boundaryDistance[neighbor] = nextDistance;
                _distanceQueue.Enqueue(neighbor);
                maxDistance = Math.Max(maxDistance, nextDistance);
            }
        }

        return maxDistance;
    }

    private bool IsBoundaryTile(Vector2i tile)
    {
        foreach (var offset in CardinalOffsets)
        {
            if (!_occupiedTiles.Contains(tile + offset))
                return true;
        }

        return false;
    }

    private ShaderInstance GetFloodShader()
    {
        if (_activeFloodShaderCount >= _floodShaders.Count)
            _floodShaders.Add(_baseFloodShader.Duplicate());

        return _floodShaders[_activeFloodShaderCount++];
    }

    private bool EnsureGridCache(
        EntityUid gridUid,
        MapGridComponent grid,
        SharedMapSystem mapSystem,
        out FloodGridCache cache)
    {
        if (!_gridCaches.TryGetValue(gridUid, out cache!))
        {
            cache = new FloodGridCache();
            _gridCaches[gridUid] = cache;
        }

        var shapeVersion = _entityManager.System<ClientShipDrowningSystem>().GetGridShapeVersion(gridUid);
        if (cache.ShapeVersion == shapeVersion && cache.MaxBoundaryDistance >= 0 && cache.OccupiedTiles.Count > 0)
            return true;

        _occupiedTiles = cache.OccupiedTiles;
        _boundaryDistance = cache.BoundaryDistance;
        _occupiedTiles.Clear();
        _boundaryDistance.Clear();
        _distanceQueue.Clear();

        var allTiles = mapSystem.GetAllTilesEnumerator(gridUid, grid);
        var minTile = new Vector2i(int.MaxValue, int.MaxValue);
        var maxTile = new Vector2i(int.MinValue, int.MinValue);
        while (allTiles.MoveNext(out var tile))
        {
            var indices = tile.Value.GridIndices;
            _occupiedTiles.Add(indices);
            minTile.X = Math.Min(minTile.X, indices.X);
            minTile.Y = Math.Min(minTile.Y, indices.Y);
            maxTile.X = Math.Max(maxTile.X, indices.X);
            maxTile.Y = Math.Max(maxTile.Y, indices.Y);
        }

        cache.ShapeVersion = shapeVersion;
        cache.MeshValid = false;
        cache.MeshVertices.Clear();

        if (_occupiedTiles.Count == 0)
        {
            cache.MinTile = Vector2i.Zero;
            cache.MaxTile = Vector2i.Zero;
            cache.MaxBoundaryDistance = -1;
            return false;
        }

        cache.MinTile = minTile;
        cache.MaxTile = maxTile;
        cache.MaxBoundaryDistance = BuildBoundaryDistances();
        return cache.MaxBoundaryDistance >= 0;
    }

    private bool NeedsFloodMeshRebuild(FloodGridCache cache, float drownRatio, Vector2 waterDrift)
    {
        if (!cache.MeshValid)
            return true;

        var occupiedTileCount = cache.OccupiedTiles.Count;
        var elapsed = (_timing.CurTime - cache.LastMeshRebuildTime).TotalSeconds;
        if (elapsed < GetMinMeshRebuildInterval(occupiedTileCount))
            return false;

        if (MathF.Abs(cache.CachedDrownRatio - drownRatio) >= GetCoverageRebuildThreshold(occupiedTileCount))
            return true;

        var driftDelta = cache.CachedWaterDrift - waterDrift;
        return driftDelta.LengthSquared() >= GetWaterDriftRebuildThresholdSquared(occupiedTileCount);
    }

    private void RebuildFloodMesh(FloodGridCache cache, int shipSeed, float drownRatio, Vector2 waterDrift)
    {
        _occupiedTiles = cache.OccupiedTiles;
        _boundaryDistance = cache.BoundaryDistance;
        _meshVertices = cache.MeshVertices;
        _tileFill.Clear();
        _vertexFill.Clear();
        _floodBlobs.Clear();
        _floodBlobBuckets.Clear();
        _meshVertices.Clear();

        var coverageRatio = Math.Clamp(drownRatio, 0f, 1f);
        var overflowRatio = Math.Clamp(drownRatio - 1f, 0f, 1.4f);
        var floodProgress = MathF.Pow(coverageRatio, 1.08f);
        var globalEdgeFill = SmoothStep(0.955f, 1.02f, coverageRatio + overflowRatio * 0.2f);
        var floodFront = MathHelper.Lerp(0.08f, cache.MaxBoundaryDistance + 0.9f, floodProgress);

        foreach (var tile in _occupiedTiles)
        {
            if (!_boundaryDistance.TryGetValue(tile, out var distance))
                continue;

            var seedA = Hash(tile.X * 928371 + tile.Y * 364479 + shipSeed * 83);
            var seedB = Hash(tile.X * 18233 + tile.Y * 74653 + shipSeed * 41 + 19);
            var frontNoise = (seedA - 0.5f) * 0.12f + (seedB - 0.5f) * 0.05f;
            var floodDepth = floodFront - distance + frontNoise;
            var fill = SmoothStep(-0.08f, 0.94f, floodDepth);

            if (IsBoundaryTile(tile))
                fill = MathF.Max(fill, globalEdgeFill * 0.96f);

            if (fill <= 0.0005f)
                continue;

            _tileFill[tile] = Math.Clamp(fill, 0f, 1.04f);
        }

        BuildVertexFills();
        BuildFloodBlobs(coverageRatio, shipSeed);
        BuildFloodMesh(
            cache.MinTile.X,
            cache.MinTile.Y,
            cache.MaxTile.X + 1f,
            cache.MaxTile.Y + 1f,
            coverageRatio,
            overflowRatio,
            globalEdgeFill,
            shipSeed,
            waterDrift,
            GetFloodMeshStep(cache.OccupiedTiles.Count));

        cache.CachedDrownRatio = drownRatio;
        cache.CachedWaterDrift = waterDrift;
        cache.LastMeshRebuildTime = _timing.CurTime;
        cache.MeshValid = true;
    }

    private void AccumulateVertexTileFill(Vector2i tile, ref float sum, ref float max, ref int count)
    {
        if (!_occupiedTiles.Contains(tile))
            return;

        var fill = GetTileFill(tile);
        if (fill <= 0f)
            return;

        count++;
        sum += fill;
        max = MathF.Max(max, fill);
    }

    private float[] EnsureSampleBuffer(int sampleCount)
    {
        if (_sampleBuffer.Length < sampleCount)
            _sampleBuffer = new float[sampleCount];

        return _sampleBuffer;
    }

    private static float GetFloodMeshStep(int occupiedTileCount)
    {
        if (occupiedTileCount >= 1600)
            return MassiveFloodMeshStep;

        if (occupiedTileCount >= 900)
            return HugeFloodMeshStep;

        if (occupiedTileCount >= 400)
            return LargeFloodMeshStep;

        if (occupiedTileCount >= 180)
            return MediumFloodMeshStep;

        return SmallFloodMeshStep;
    }

    private static float GetMinMeshRebuildInterval(int occupiedTileCount)
    {
        if (occupiedTileCount >= 900)
            return 0.3f;

        if (occupiedTileCount >= 400)
            return 0.18f;

        if (occupiedTileCount >= 180)
            return 0.1f;

        return MinMeshRebuildInterval;
    }

    private static float GetCoverageRebuildThreshold(int occupiedTileCount)
    {
        if (occupiedTileCount >= 900)
            return 0.028f;

        if (occupiedTileCount >= 400)
            return 0.018f;

        if (occupiedTileCount >= 180)
            return 0.012f;

        return CoverageRebuildThreshold;
    }

    private static float GetWaterDriftRebuildThresholdSquared(int occupiedTileCount)
    {
        if (occupiedTileCount >= 900)
            return 0.0025f;

        if (occupiedTileCount >= 400)
            return 0.0016f;

        if (occupiedTileCount >= 180)
            return 0.0009f;

        return WaterDriftRebuildThresholdSquared;
    }

    private float GetTileFill(Vector2i tile)
    {
        return _tileFill.GetValueOrDefault(tile, 0f);
    }
    private static float ContinuousFloodNoise(Vector2 position, int shipSeed)
    {
        var seed = shipSeed * 0.031f;
        var layerA =
            MathF.Sin(position.X * 1.43f + seed + MathF.Sin(position.Y * 0.61f - seed * 1.17f));
        var layerB =
            MathF.Cos(position.Y * 1.76f - seed * 0.79f + MathF.Cos((position.X + position.Y) * 0.53f + seed));
        var layerC =
            MathF.Sin((position.X - position.Y) * 2.07f + seed * 1.83f);
        return layerA * 0.5f + layerB * 0.34f + layerC * 0.16f;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) < float.Epsilon)
            return value >= edge1 ? 1f : 0f;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Hash(int value)
    {
        var hashed = MathF.Sin(value * 12.9898f) * 43758.5453f;
        return hashed - MathF.Floor(hashed);
    }
}
