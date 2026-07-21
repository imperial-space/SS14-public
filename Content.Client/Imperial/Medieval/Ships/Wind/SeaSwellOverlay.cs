using System.Numerics;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed class SeaSwellOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float TileQueryPadding = 4f;
    private const float ParticleSpawnPadding = 1.2f;
    private const float CalmParticleDensity = 0.2f;
    private const float StormParticleDensity = 0.58f;
    private const int CalmMinParticles = 44;
    private const int StormMinParticles = 132;
    private const int CalmMaxParticles = 128;
    private const int StormMaxParticles = 360;
    private const float ParticleOverlapPadding = 0.035f;
    private const float CalmSizeScale = 0.72f;
    private const float StormSizeScale = 1.18f;
    private const float CalmSpreadScale = 0.78f;
    private const float StormSpreadScale = 1.16f;
    private const float CalmThicknessScale = 0.8f;
    private const float StormThicknessScale = 1.18f;
    private const float CalmAlphaScale = 0.72f;
    private const float StormAlphaScale = 1.1f;
    private const float CalmLifetimeScale = 1f;
    private const float StormLifetimeScale = 1.06f;
    private const float CalmAnimationSpeed = 1f;
    private const float StormAnimationSpeed = 1.24f;
    private const float CalmLineChance = 0.38f;
    private const float StormLineChance = 0.16f;
    private const float CalmChevronChance = 0.28f;
    private const float StormChevronChance = 0.34f;

    private readonly Dictionary<MapId, List<SwellParticle>> _particlesByMap = new();
    private readonly List<MapId> _staleMaps = new();
    private readonly ShaderInstance _unshadedShader;
    private float _stormStrength;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public SeaSwellOverlay()
    {
        IoCManager.InjectDependencies(this);
        _unshadedShader = _prototypeManager.Index(UnshadedShader).Instance();
        ZIndex = 13;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) || sea.Disabled)
            return false;

        if (_configuration.GetCVar(ShipsCCVars.WindEnabled))
            return true;

        return _particlesByMap.TryGetValue(args.MapId, out var particles) && particles.Count > 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var rawStrength = _configuration.GetCVar(ShipsCCVars.WindEnabled)
            ? MathF.Max(0f, _configuration.GetCVar(ShipsCCVars.StormLevel))
            : 0f;
        var targetStrength = Math.Clamp(rawStrength / 10f, 0f, 1f);
        _stormStrength += (targetStrength - _stormStrength) * MathF.Min(1f, args.DeltaSeconds * 2f);

        _staleMaps.Clear();
        var animationSpeed = MathHelper.Lerp(CalmAnimationSpeed, StormAnimationSpeed, _stormStrength);

        foreach (var (mapId, particles) in _particlesByMap)
        {
            for (var i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.Age += args.DeltaSeconds * animationSpeed;

                if (particle.Age >= particle.Lifetime)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                particles[i] = particle;
            }

            if (particles.Count == 0)
                _staleMaps.Add(mapId);
        }

        foreach (var mapId in _staleMaps)
        {
            _particlesByMap.Remove(mapId);
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var visibleBounds = args.WorldAABB.Enlarged(TileQueryPadding);

        var particles = AllParticles(args.MapId);
        if (_configuration.GetCVar(ShipsCCVars.WindEnabled))
            SpawnParticles(args.MapId, particles, visibleBounds);
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        args.WorldHandle.UseShader(_unshadedShader);
        DrawParticles(args.WorldHandle, args.MapId, particles, args.WorldAABB.Enlarged(ParticleSpawnPadding + _stormStrength * 0.35f), eyeRotation);
        args.WorldHandle.UseShader(null);
    }

    private List<SwellParticle> AllParticles(MapId mapId)
    {
        if (_particlesByMap.TryGetValue(mapId, out var particles))
            return particles;

        particles = new List<SwellParticle>();
        _particlesByMap[mapId] = particles;
        return particles;
    }

    private void SpawnParticles(MapId mapId, List<SwellParticle> particles, Box2 visibleBounds)
    {
        var spawnBounds = GetSpawnFocusBounds(visibleBounds);
        var area = visibleBounds.Width * visibleBounds.Height;
        var density = MathHelper.Lerp(CalmParticleDensity, StormParticleDensity, _stormStrength);
        var minParticles = (int) MathF.Round(MathHelper.Lerp(CalmMinParticles, StormMinParticles, _stormStrength));
        var maxParticles = (int) MathF.Round(MathHelper.Lerp(CalmMaxParticles, StormMaxParticles, _stormStrength));
        var targetCount = Math.Clamp((int) MathF.Ceiling(area * density), minParticles, maxParticles);
        var slotCount = 0;

        for (var i = particles.Count - 1; i >= 0; i--)
        {
            var particle = particles[i];
            if (!visibleBounds.Contains(particle.Position))
                continue;

            var reach = EstimateParticleReach(particle.Shape, particle.Length, particle.Spread);
            if (IsParticleBlocked(mapId, particle.Position, reach))
            {
                particles.RemoveAt(i);
                continue;
            }

            slotCount++;
        }

        var missing = targetCount - slotCount;
        var warmStart = particles.Count == 0;

        for (var i = 0; i < missing; i++)
        {
            if (TryCreateParticle(mapId, particles, spawnBounds, warmStart, out var particle))
                particles.Add(particle);
        }
    }

    private bool TryCreateParticle(MapId mapId, List<SwellParticle> particles, Box2 spawnBounds, bool warmStart, out SwellParticle particle)
    {
        var sizeScale = MathHelper.Lerp(CalmSizeScale, StormSizeScale, _stormStrength);
        var spreadScale = MathHelper.Lerp(CalmSpreadScale, StormSpreadScale, _stormStrength);
        var thicknessScale = MathHelper.Lerp(CalmThicknessScale, StormThicknessScale, _stormStrength);
        var alphaScale = MathHelper.Lerp(CalmAlphaScale, StormAlphaScale, _stormStrength);
        var lifetimeScale = MathHelper.Lerp(CalmLifetimeScale, StormLifetimeScale, _stormStrength);
        var lineChance = MathHelper.Lerp(CalmLineChance, StormLineChance, _stormStrength);
        var chevronChance = MathHelper.Lerp(CalmChevronChance, StormChevronChance, _stormStrength);

        for (var attempt = 0; attempt < 44; attempt++)
        {
            var position = new Vector2(
                _random.NextFloat(spawnBounds.Left, spawnBounds.Right),
                _random.NextFloat(spawnBounds.Bottom, spawnBounds.Top));

            var shapeRoll = _random.NextFloat();
            SwellShapeType shape;
            if (shapeRoll < lineChance)
                shape = SwellShapeType.LinePulse;
            else if (shapeRoll < lineChance + chevronChance)
                shape = SwellShapeType.Chevron;
            else
                shape = SwellShapeType.Split;
            var lifetime = _random.NextFloat(1.15f, 1.95f) * lifetimeScale;
            var (length, spread, thickness, alpha) = shape switch
            {
                SwellShapeType.Chevron => (
                    _random.NextFloat(0.11f, 0.22f),
                    _random.NextFloat(0.024f, 0.06f),
                    _random.NextFloat(0.0042f, 0.0068f),
                    _random.NextFloat(0.3f, 0.44f)),
                SwellShapeType.Split => (
                    _random.NextFloat(0.18f, 0.34f),
                    _random.NextFloat(0.06f, 0.15f),
                    _random.NextFloat(0.0045f, 0.0073f),
                    _random.NextFloat(0.36f, 0.54f)),
                _ => (
                    _random.NextFloat(0.16f, 0.3f),
                    _random.NextFloat(0.04f, 0.095f),
                    _random.NextFloat(0.0044f, 0.007f),
                    _random.NextFloat(0.34f, 0.5f)),
            };
            length *= sizeScale;
            spread *= spreadScale;
            thickness *= thicknessScale;
            alpha *= alphaScale;
            var reach = EstimateParticleReach(shape, length, spread);

            if (IsParticleBlocked(mapId, position, reach))
                continue;

            if (OverlapsExistingParticle(particles, position, reach))
                continue;

            particle = new SwellParticle
            {
                Position = position,
                Age = warmStart
                    ? _random.NextFloat(0f, lifetime * 0.72f)
                    : -_random.NextFloat(0f, lifetime * 0.16f),
                Lifetime = lifetime,
                Length = length,
                Spread = spread,
                Thickness = thickness,
                Alpha = alpha,
                AngleOffset = _random.NextFloat(-0.045f, 0.045f),
                Shape = shape,
            };

            return true;
        }

        particle = default;
        return false;
    }

    private static Box2 GetSpawnFocusBounds(Box2 visibleBounds)
    {
        var insetX = MathF.Min(visibleBounds.Width * 0.08f, 1.25f);
        var insetY = MathF.Min(visibleBounds.Height * 0.08f, 1f);

        if (visibleBounds.Width <= insetX * 2f || visibleBounds.Height <= insetY * 2f)
            return visibleBounds;

        return new Box2(
            visibleBounds.Left + insetX,
            visibleBounds.Bottom + insetY,
            visibleBounds.Right - insetX,
            visibleBounds.Top - insetY);
    }

    private static bool OverlapsExistingParticle(List<SwellParticle> particles, Vector2 position, float reach)
    {
        var minDistance = reach + ParticleOverlapPadding;

        foreach (var particle in particles)
        {
            var otherReach = ParticleOverlapPadding + EstimateParticleReach(particle.Shape, particle.Length, particle.Spread);
            var combinedDistance = minDistance + otherReach;

            if ((particle.Position - position).LengthSquared() < combinedDistance * combinedDistance)
                return true;
        }

        return false;
    }

    private bool IsParticleBlocked(MapId mapId, Vector2 position, float radius)
    {
        return IsBlockedByTile(mapId, position) ||
               IsBlockedByTile(mapId, position + new Vector2(radius, 0f)) ||
               IsBlockedByTile(mapId, position - new Vector2(radius, 0f)) ||
               IsBlockedByTile(mapId, position + new Vector2(0f, radius)) ||
               IsBlockedByTile(mapId, position - new Vector2(0f, radius));
    }

    private void DrawParticles(DrawingHandleWorld handle, MapId mapId, List<SwellParticle> particles, Box2 drawBounds, Angle eyeRotation)
    {
        var screenRight = (-eyeRotation).ToVec();
        var screenUp = new Vector2(-screenRight.Y, screenRight.X);

        foreach (var particle in particles)
        {
            if (particle.Age < 0f)
                continue;

            if (!drawBounds.Contains(particle.Position))
                continue;

            var life = Math.Clamp(particle.Age / MathF.Max(particle.Lifetime, 0.001f), 0f, 1f);
            var alpha = particle.Alpha * EstimateVisibility(life);

            if (alpha <= 0.002f)
                continue;

            var color = Color.White.WithAlpha(alpha);

            switch (particle.Shape)
            {
                case SwellShapeType.Chevron:
                    DrawChevron(handle, mapId, particle, life, screenRight, screenUp, color);
                    break;
                case SwellShapeType.Split:
                    DrawSplit(handle, mapId, particle, life, screenRight, screenUp, color);
                    break;
                default:
                    DrawLinePulse(handle, mapId, particle, life, screenRight, screenUp, color);
                    break;
            }
        }
    }

    private void DrawLinePulse(
        DrawingHandleWorld handle,
        MapId mapId,
        SwellParticle particle,
        float life,
        Vector2 screenRight,
        Vector2 screenUp,
        Color color)
    {
        var pulse = SizeEnvelope(life);
        var direction = Rotate(screenRight, particle.AngleOffset);
        var center = particle.Position;
        var halfLength = particle.Length * pulse * 0.62f;
        var thickness = particle.Thickness * (0.28f + pulse * 0.88f);

        var points = new Vector2[2];
        points[0] = center - direction * halfLength;
        points[1] = center + direction * halfLength;
        DrawPolylineRibbon(handle, mapId, points, thickness, color);
    }

    private void DrawChevron(
        DrawingHandleWorld handle,
        MapId mapId,
        SwellParticle particle,
        float life,
        Vector2 screenRight,
        Vector2 screenUp,
        Color color)
    {
        var openPhase = SizeEnvelope(life);
        var risePhase = CrestEnvelope(life);
        var direction = Rotate(screenRight, particle.AngleOffset * 0.5f);
        var center = particle.Position;
        var halfLength = particle.Length * openPhase * 0.56f;
        var lift = particle.Spread * risePhase * 1.42f;
        var thickness = particle.Thickness * (0.26f + openPhase * 0.86f);

        var points = new Vector2[3];
        points[0] = center - direction * halfLength;
        points[1] = center + screenUp * lift;
        points[2] = center + direction * halfLength;

        DrawPolylineRibbon(handle, mapId, points, thickness, color);
    }

    private void DrawSplit(
        DrawingHandleWorld handle,
        MapId mapId,
        SwellParticle particle,
        float life,
        Vector2 screenRight,
        Vector2 screenUp,
        Color color)
    {
        var branchPhase = SplitBranchEnvelope(life);
        var separationPhase = SplitSeparationEnvelope(life);
        var direction = Rotate(screenRight, particle.AngleOffset * 0.45f);
        var center = particle.Position;
        var separation = particle.Spread * separationPhase * 2.85f;
        var halfLength = particle.Length * branchPhase * 0.47f;
        var thickness = particle.Thickness * (0.28f + branchPhase * 0.86f);

        var left = new Vector2[2];
        var right = new Vector2[2];

        var leftCenter = center - direction * separation;
        var rightCenter = center + direction * separation;
        left[0] = leftCenter - direction * halfLength;
        left[1] = leftCenter + direction * halfLength;
        right[0] = rightCenter - direction * halfLength;
        right[1] = rightCenter + direction * halfLength;

        DrawPolylineRibbon(handle, mapId, left, thickness, color);
        DrawPolylineRibbon(handle, mapId, right, thickness, color);
    }

    private void DrawPolylineRibbon(DrawingHandleWorld handle, MapId mapId, ReadOnlySpan<Vector2> points, float thickness, Color color)
    {
        if (points.Length < 2)
            return;

        var vertices = new Vector2[(points.Length - 1) * 6];
        var vertexIndex = 0;

        for (var i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            if (IsBlockedSegment(mapId, a, b))
                continue;

            var startT = i / (float) (points.Length - 1);
            var endT = (i + 1) / (float) (points.Length - 1);
            var startWidth = RibbonHalfWidth(thickness, startT);
            var endWidth = RibbonHalfWidth(thickness, endT);
            var tangent = b - a;
            if (tangent.LengthSquared() <= 0.00001f)
                continue;

            var normal = Vector2.Normalize(new Vector2(-tangent.Y, tangent.X));
            var aLeft = a - normal * startWidth;
            var aRight = a + normal * startWidth;
            var bLeft = b - normal * endWidth;
            var bRight = b + normal * endWidth;

            vertices[vertexIndex++] = aLeft;
            vertices[vertexIndex++] = aRight;
            vertices[vertexIndex++] = bRight;
            vertices[vertexIndex++] = aLeft;
            vertices[vertexIndex++] = bRight;
            vertices[vertexIndex++] = bLeft;
        }

        if (vertexIndex == 0)
            return;

        if (vertexIndex != vertices.Length)
            Array.Resize(ref vertices, vertexIndex);

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
    }

    private bool IsBlockedSegment(MapId mapId, Vector2 a, Vector2 b)
    {
        return IsBlockedByTile(mapId, a) ||
               IsBlockedByTile(mapId, Vector2.Lerp(a, b, 0.25f)) ||
               IsBlockedByTile(mapId, Vector2.Lerp(a, b, 0.5f)) ||
               IsBlockedByTile(mapId, Vector2.Lerp(a, b, 0.75f)) ||
               IsBlockedByTile(mapId, b);
    }

    private bool IsBlockedByTile(MapId mapId, Vector2 worldPoint)
    {
        return _mapManager.TryFindGridAt(mapId, worldPoint, out _, out _);
    }

    private static float RibbonHalfWidth(float baseThickness, float pointT)
    {
        var edgeFade = MathF.Sin(pointT * MathF.PI);
        return baseThickness * (0.22f + edgeFade * 0.86f);
    }

    private static float EstimateParticleReach(SwellShapeType shape, float length, float spread)
    {
        return shape switch
        {
            SwellShapeType.Chevron => MathF.Max(length * 0.65f, spread * 1.9f),
            SwellShapeType.Split => MathF.Max(length * 0.48f + spread * 2.7f, spread * 3.1f),
            _ => MathF.Max(length * 0.7f, spread * 1.2f),
        };
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) < float.Epsilon)
            return value >= edge1 ? 1f : 0f;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float SizeEnvelope(float life)
    {
        var appear = SmoothStep(0f, 0.22f, life);
        var vanish = 1f - SmoothStep(0.52f, 0.96f, life);
        return MathF.Pow(appear * vanish, 0.82f);
    }

    private static float CrestEnvelope(float life)
    {
        var appear = SmoothStep(0.06f, 0.3f, life);
        var vanish = 1f - SmoothStep(0.48f, 0.9f, life);
        return MathF.Pow(appear * vanish, 0.9f);
    }

    private static float EstimateVisibility(float life)
    {
        var appear = SmoothStep(0f, 0.16f, life);
        var vanish = 1f - SmoothStep(0.64f, 1f, life);
        return appear * vanish;
    }

    private static float SplitBranchEnvelope(float life)
    {
        var appear = SmoothStep(0f, 0.2f, life);
        var vanish = 1f - SmoothStep(0.56f, 0.98f, life);
        return MathF.Pow(appear * vanish, 0.76f);
    }

    private static float SplitSeparationEnvelope(float life)
    {
        var expand = SmoothStep(0.02f, 0.42f, life);
        var settle = 1f - SmoothStep(0.9f, 1f, life);
        return MathF.Pow(expand * settle, 0.9f);
    }

    private struct SwellParticle
    {
        public Vector2 Position;
        public float Age;
        public float Lifetime;
        public float Length;
        public float Spread;
        public float Thickness;
        public float Alpha;
        public float AngleOffset;
        public SwellShapeType Shape;
    }

    private enum SwellShapeType : byte
    {
        LinePulse,
        Chevron,
        Split,
    }
}
