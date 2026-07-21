using System.Numerics;
using Content.Client.Imperial.Medieval.ShipDrowning;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed partial class SeaShipRippleOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> RippleShader = "SeaShipRipple";
    private static readonly ProtoId<ShaderPrototype> RippleDetailLitShader = "SeaShipRippleDetailLit";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int EdgePointCount = 24;
    private const float RippleOffset = 0.106f;
    private const float RippleBandExtent = 0.162f;
    private const float RippleAmplitude = 0.032f;
    private const float RippleScrollSpeed = 1.75f;
    private const float RippleAlpha = 0.58f;
    private const float TileQueryPadding = 3f;
    private const float ConcavePatchSize = 0.11f;
    private const int ConvexCornerSegments = 10;
    private const float ConvexCornerOverlap = 0.0015f;
    private const int ArcPointCount = 18;
    private const int DetachedWaveletPointCount = 8;
    private const float MovementWaveMinSpeed = 0.14f;
    private const float SmallWaveMinDelay = 0.16f;
    private const float SmallWaveMaxDelay = 0.34f;
    private const float MovingWaveMinDelay = 0.28f;
    private const float MovingWaveMaxDelay = 0.46f;
    private const float MovementWakeMinWidth = 0.35f;
    private const float MovementWakeWidthPadding = 0.08f;
    private const float MovementWakeMinFrequencyScale = 0.18f;
    private const float MovementWakeMaxDelayScale = 2.9f;
    private const float MovementWakeMaxLifetimeScale = 1.88f;
    private const float MovementWakeBaseAftOffset = 0.16f;
    private const float MovementWakeWidthAftOffset = 0.075f;
    private const float MovementWakeLowSpeedAftBoost = 0.11f;

    private HashSet<Vector2i> _occupiedTiles = new();
    private readonly HashSet<Vector2i> _visibleOccupiedTiles = new();
    private readonly HashSet<Vector2i> _visitedVertices = new();
    private readonly Dictionary<EntityUid, ShipMotionState> _shipStates = new();
    private readonly Dictionary<EntityUid, ShipMask> _shipMasks = new();
    private readonly HashSet<EntityUid> _activeShips = new();
    private readonly List<WaveParticle> _waveParticles = new();
    private readonly List<RippleEmitPoint> _emitPoints = new();
    private Matrix3x2 _emitWorldMatrix = Matrix3x2.Identity;
    private readonly ShaderInstance _baseRippleShader;
    private readonly ShaderInstance _detailRippleShader;
    private readonly List<ShaderInstance> _rippleShaders = new();
    private readonly Texture _whiteTexture;
    private int _activeRippleShaderCount;

    private SharedMapSystem MapSystem => _entityManager.System<SharedMapSystem>();

    private sealed class ShipMask
    {
        public EntityUid GridUid;
        public MapGridComponent Grid = default!;
        public Box2 WorldBounds;
        public Matrix3x2 WorldMatrix = Matrix3x2.Identity;
        public int ShapeVersion = -1;
        public readonly HashSet<Vector2i> OccupiedTiles = new();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public SeaShipRippleOverlay()
    {
        IoCManager.InjectDependencies(this);
        _baseRippleShader = _prototypeManager.Index(RippleShader).Instance().Duplicate();
        _detailRippleShader = _prototypeManager.Index(RippleDetailLitShader).Instance().Duplicate();
        _whiteTexture = Texture.White;
        ZIndex = 15;
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        _baseRippleShader.Dispose();
        _detailRippleShader.Dispose();

        foreach (var shader in _rippleShaders)
        {
            shader.Dispose();
        }

        _rippleShaders.Clear();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        for (var i = _waveParticles.Count - 1; i >= 0; i--)
        {
            var particle = _waveParticles[i];
            particle.Age += args.DeltaSeconds;
            particle.Center += particle.Velocity * args.DeltaSeconds;
            particle.Radius += particle.RadiusGrowth * args.DeltaSeconds;
            particle.Thickness += particle.ThicknessGrowth * args.DeltaSeconds;
            particle.Span += particle.SpanGrowth * args.DeltaSeconds;

            if (particle.Age >= particle.Lifetime)
                _waveParticles.RemoveAt(i);
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) && !sea.Disabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var visibleBounds = args.WorldAABB.Enlarged(TileQueryPadding);
        var xformSystem = _entityManager.System<SharedTransformSystem>();
        var lookupSystem = _entityManager.System<EntityLookupSystem>();
        var query = _entityManager.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
        _activeShips.Clear();
        _activeRippleShaderCount = 0;

        while (query.MoveNext(out var uid, out var grid, out var xform))
        {
            if (!_entityManager.HasComponent<ShipDrowningComponent>(uid))
                continue;

            if (xform.MapID != args.MapId)
                continue;

            var worldMatrix = xformSystem.GetWorldMatrix(xform);
            var worldBounds = worldMatrix.TransformBox(grid.LocalAABB);

            if (!worldBounds.Intersects(visibleBounds))
                continue;

            var shipMask = EnsureShipMask(uid, grid, worldBounds);
            if (shipMask.OccupiedTiles.Count == 0)
                continue;

            _activeShips.Add(uid);
            var motion = UpdateShipMotion(uid, xformSystem.GetWorldPosition(uid));
            DrawRipple(args.WorldHandle, args.MapId, uid, grid, worldMatrix, visibleBounds, shipMask, lookupSystem, motion);
        }

        var staleShips = new List<EntityUid>();
        foreach (var (uid, _) in _shipStates)
        {
            if (!_activeShips.Contains(uid))
                staleShips.Add(uid);
        }

        foreach (var uid in staleShips)
        {
            _shipStates.Remove(uid);
        }

        staleShips.Clear();
        foreach (var (uid, _) in _shipMasks)
        {
            if (!_activeShips.Contains(uid))
                staleShips.Add(uid);
        }

        foreach (var uid in staleShips)
        {
            _shipMasks.Remove(uid);
        }

        DrawWaveParticles(args.WorldHandle, args.MapId);
    }

    private ShipMask EnsureShipMask(
        EntityUid gridUid,
        MapGridComponent grid,
        Box2 worldBounds)
    {
        if (!_shipMasks.TryGetValue(gridUid, out var shipMask))
        {
            shipMask = new ShipMask();
            _shipMasks.Add(gridUid, shipMask);
        }

        shipMask.GridUid = gridUid;
        shipMask.Grid = grid;
        shipMask.WorldBounds = worldBounds;
        shipMask.WorldMatrix = _entityManager.System<SharedTransformSystem>().GetWorldMatrix(gridUid);
        var shapeVersion = _entityManager.System<ClientShipDrowningSystem>().GetGridShapeVersion(gridUid);
        if (shipMask.ShapeVersion == shapeVersion)
            return shipMask;

        shipMask.ShapeVersion = shapeVersion;
        shipMask.OccupiedTiles.Clear();

        var tileEnumerator = MapSystem.GetTilesEnumerator(gridUid, grid, worldBounds);
        while (tileEnumerator.MoveNext(out var tileRef))
        {
            shipMask.OccupiedTiles.Add(tileRef.GridIndices);
        }

        return shipMask;
    }
}
