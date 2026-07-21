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
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Ships.Wind;

public sealed class SeaWindOverlay : Overlay
{
    private const int OverlayZIndex = 100;
    private const int CurvePointCount = 10;
    private const int VerticesPerSegment = 6;

    private const float DegreesToRadians = MathF.PI / 180f;
    private const float FullRotation = MathF.PI * 2f;
    private const float StrengthScale = 10f;
    private const float VisibleStrengthThreshold = 0.01f;
    private const float AngleSmoothSpeed = 4f;
    private const float StrengthSmoothSpeed = 2f;

    private const float ParticleDensityBase = 0.112f;
    private const float ParticleDensityStrength = 0.0325f;
    private const int MinParticleCount = 34;
    private const int MaxParticleCount = 220;
    private const float ActiveBoundsBasePadding = 1.8f;
    private const float ActiveBoundsStrengthPadding = 2.8f;
    private const float DrawBoundsExtraPadding = 2f;

    private const float AngleJitter = 0.1f;
    private const float SpeedBaseMin = 8.25f;
    private const float SpeedBaseMax = 14.35f;
    private const float SpeedStrengthMin = 0.375f;
    private const float SpeedStrengthMax = 0.6875f;
    private const float TravelDistanceBaseMin = 38f;
    private const float TravelDistanceBaseMax = 60f;
    private const float TravelDistanceStrengthMin = 1.125f;
    private const float TravelDistanceStrengthMax = 2f;
    private const float LengthBaseMin = 1.3525f;
    private const float LengthBaseMax = 2.8325f;
    private const float LengthStrengthMin = 0.04296875f;
    private const float LengthStrengthMax = 0.1055f;
    private const float MinimumSpeed = 0.1f;

    private const float SpawnMarginBase = 9.6f;
    private const float SpawnMarginStrength = 0.375f;
    private const float SpawnDirectionMinOffset = 0.5f;
    private const float SpawnLateralPadding = 2f;
    private const float SpawnDirectionJitter = 0.5f;

    private const float CurveMin = -0.14f;
    private const float CurveMax = 0.14f;
    private const float CurveBiasMin = -0.2f;
    private const float CurveBiasMax = 0.2f;
    private const float AlphaMin = 0.45f;
    private const float AlphaMax = 0.95f;
    private const float WidthScaleMin = 0.9f;
    private const float WidthScaleMax = 1.22f;
    private const float CurveVelocityScale = 1.35f;
    private const float WarmStartLifetimeScale = 0.65f;
    private const float WarmStartVisibleTravelScale = 0.8f;
    private const float VisibleTravelExtentScale = 1.45f;

    private const float FadeInEnd = 0.18f;
    private const float FadeOutStart = 0.82f;
    private const float SpawnFadeEnd = 0.28f;
    private const float AlphaBaseStrength = 0.18f;
    private const float AlphaWindStrength = 0.4f;
    private const float MinimumAlpha = 0.001f;
    private const float MinimumFadeWidth = 0.001f;

    private const float RibbonBaseHalfWidth = 0.004f;
    private const float RibbonStrengthHalfWidth = 0.0032f;
    private const float RibbonCenterHalfWidth = 0.0064f;
    private const float MinimumTangentLengthSquared = 0.0001f;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<MapId, List<WindParticle>> _particlesByMap = new();
    private readonly List<MapId> _mapsToRemove = new();

    private float _windAngle;
    private float _windStrength;
    private float _windPower;
    private float _stormLevel;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public SeaWindOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = OverlayZIndex;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var targetAngle = _configuration.GetCVar(ShipsCCVars.WindRotation) * DegreesToRadians;
        var windPower = MathF.Max(0f, _configuration.GetCVar(ShipsCCVars.WindPower));
        var stormLevel = MathF.Max(0f, _configuration.GetCVar(ShipsCCVars.StormLevel));
        var targetStrength = NormalizeStrength(MathF.Max(windPower, stormLevel));
        var angleDelta = NormalizeAngle(targetAngle - _windAngle);

        _windPower = windPower;
        _stormLevel = stormLevel;
        _windAngle = NormalizeAngle(_windAngle + angleDelta * SmoothRatio(args.DeltaSeconds, AngleSmoothSpeed));
        _windStrength = Approach(_windStrength, targetStrength, args.DeltaSeconds, StrengthSmoothSpeed);

        UpdateParticles(args.DeltaSeconds);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) || sea.Disabled)
            return false;

        return ShouldSpawnParticles(sea) ||
               _particlesByMap.TryGetValue(args.MapId, out var particles) && particles.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var particles = GetParticles(args.MapId);
        var visibleBounds = args.WorldAABB;

        if (_entityManager.TryGetComponent<SeaComponent>(args.MapUid, out var sea) && ShouldSpawnParticles(sea))
            SpawnParticles(particles, visibleBounds);

        DrawParticles(args.WorldHandle, particles, visibleBounds);
    }

    private bool ShouldSpawnParticles(SeaComponent sea)
    {
        return sea.WindEnabledLocal && _configuration.GetCVar(ShipsCCVars.WindEnabled) && _windStrength > VisibleStrengthThreshold;
    }

    private List<WindParticle> GetParticles(MapId mapId)
    {
        if (_particlesByMap.TryGetValue(mapId, out var particles))
            return particles;

        particles = new List<WindParticle>();
        _particlesByMap[mapId] = particles;
        return particles;
    }

    private void UpdateParticles(float frameTime)
    {
        _mapsToRemove.Clear();

        foreach (var (mapId, particles) in _particlesByMap)
        {
            for (var i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.Age += frameTime;
                particle.SpawnAge += frameTime;
                particle.Position += GetParticleVelocity(particle) * frameTime;

                if (particle.Age >= particle.Lifetime)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                particles[i] = particle;
            }

            if (particles.Count == 0)
                _mapsToRemove.Add(mapId);
        }

        foreach (var mapId in _mapsToRemove)
        {
            _particlesByMap.Remove(mapId);
        }
    }

    private static Vector2 GetParticleVelocity(WindParticle particle)
    {
        var moveRatio = Math.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
        var curveDrift = MathF.Sin(moveRatio * MathF.PI);
        return particle.Velocity + particle.CurveVelocity * curveDrift;
    }

    private void SpawnParticles(List<WindParticle> particles, Box2 visibleBounds)
    {
        var area = visibleBounds.Width * visibleBounds.Height;
        var intensity = GetIntensity();
        var density = ParticleDensityBase + intensity * ParticleDensityStrength;
        var targetCount = Math.Clamp((int) MathF.Ceiling(area * density), MinParticleCount, MaxParticleCount);
        var activeBounds = GetActiveBounds(visibleBounds);
        var missingParticles = targetCount - CountParticlesInBounds(particles, activeBounds);

        for (var i = 0; i < missingParticles; i++)
        {
            particles.Add(CreateParticle(visibleBounds));
        }
    }

    private Box2 GetActiveBounds(Box2 visibleBounds)
    {
        return visibleBounds.Enlarged(ActiveBoundsBasePadding + _windStrength * ActiveBoundsStrengthPadding);
    }

    private static int CountParticlesInBounds(List<WindParticle> particles, Box2 bounds)
    {
        var count = 0;

        foreach (var particle in particles)
        {
            if (bounds.Contains(particle.Position))
                count++;
        }

        return count;
    }

    private WindParticle CreateParticle(Box2 visibleBounds)
    {
        var angle = new Angle(_windAngle + _random.NextFloat(-AngleJitter, AngleJitter));
        var direction = angle.ToWorldVec();
        var perpendicular = new Vector2(-direction.Y, direction.X);
        var bounds = GetParticleSpawnBounds(visibleBounds, direction, perpendicular);
        var intensity = GetIntensity();

        var speed = RandomRange(SpeedBaseMin, SpeedBaseMax) + intensity * RandomRange(SpeedStrengthMin, SpeedStrengthMax);
        var travelDistance = RandomRange(TravelDistanceBaseMin, TravelDistanceBaseMax) +
                             intensity * RandomRange(TravelDistanceStrengthMin, TravelDistanceStrengthMax);
        var lifetime = travelDistance / MathF.Max(speed, MinimumSpeed);
        var length = RandomRange(LengthBaseMin, LengthBaseMax) + intensity * RandomRange(LengthStrengthMin, LengthStrengthMax);
        var spawnMargin = SpawnMarginBase + intensity * SpawnMarginStrength;
        var position = CreateSpawnPosition(visibleBounds, bounds, direction, perpendicular, spawnMargin);
        var particle = new WindParticle
        {
            Position = position,
            Velocity = direction * speed,
            Direction = direction,
            Lifetime = lifetime,
            Length = length,
            Curve = RandomRange(CurveMin, CurveMax) * length,
            CurveBias = RandomRange(CurveBiasMin, CurveBiasMax) * length,
            Alpha = RandomRange(AlphaMin, AlphaMax),
            WidthScale = RandomRange(WidthScaleMin, WidthScaleMax),
        };

        particle.CurveVelocity = perpendicular * (particle.Curve / MathF.Max(lifetime, MinimumSpeed)) * CurveVelocityScale;
        WarmStartParticle(ref particle, bounds.DirectionExtent, spawnMargin, speed);

        return particle;
    }

    private float GetIntensity()
    {
        return _windPower * _stormLevel;
    }

    private ParticleSpawnBounds GetParticleSpawnBounds(Box2 visibleBounds, Vector2 direction, Vector2 perpendicular)
    {
        var halfWidth = visibleBounds.Width * 0.5f;
        var halfHeight = visibleBounds.Height * 0.5f;

        return new ParticleSpawnBounds
        {
            DirectionExtent = MathF.Abs(direction.X) * halfWidth + MathF.Abs(direction.Y) * halfHeight,
            LateralExtent = MathF.Abs(perpendicular.X) * halfWidth + MathF.Abs(perpendicular.Y) * halfHeight,
        };
    }

    private Vector2 CreateSpawnPosition(
        Box2 visibleBounds,
        ParticleSpawnBounds bounds,
        Vector2 direction,
        Vector2 perpendicular,
        float spawnMargin)
    {
        return visibleBounds.Center
               - direction * (bounds.DirectionExtent + RandomRange(SpawnDirectionMinOffset, spawnMargin))
               + perpendicular * RandomRange(-bounds.LateralExtent - SpawnLateralPadding, bounds.LateralExtent + SpawnLateralPadding)
               + direction * RandomRange(-SpawnDirectionJitter, SpawnDirectionJitter);
    }

    private void WarmStartParticle(
        ref WindParticle particle,
        float directionExtent,
        float spawnMargin,
        float speed)
    {
        var visibleTravelTime = (directionExtent * VisibleTravelExtentScale + spawnMargin) / MathF.Max(speed, MinimumSpeed);
        particle.Age = RandomRange(0f, MathF.Min(particle.Lifetime * WarmStartLifetimeScale, visibleTravelTime * WarmStartVisibleTravelScale));
        particle.Position += particle.Velocity * particle.Age;

        var moveRatio = Math.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
        var curveIntegral = particle.Lifetime / MathF.PI * (1f - MathF.Cos(moveRatio * MathF.PI));
        particle.Position += particle.CurveVelocity * curveIntegral;
    }

    private void DrawParticles(DrawingHandleWorld handle, List<WindParticle> particles, Box2 visibleBounds)
    {
        var drawBounds = GetActiveBounds(visibleBounds).Enlarged(DrawBoundsExtraPadding);

        foreach (var particle in particles)
        {
            if (!drawBounds.Contains(particle.Position))
                continue;

            var alpha = GetParticleAlpha(particle, visibleBounds, drawBounds);
            if (alpha <= MinimumAlpha)
                continue;

            var curve = GetCurvePoints(particle);
            DrawRibbon(handle, curve, particle, Color.White.WithAlpha(alpha));
        }
    }

    private float GetParticleAlpha(WindParticle particle, Box2 visibleBounds, Box2 drawBounds)
    {
        var lifeRatio = particle.Age / particle.Lifetime;
        var fadeIn = SmoothStep(0f, FadeInEnd, lifeRatio);
        var fadeOut = 1f - SmoothStep(FadeOutStart, 1f, lifeRatio);
        var edgeFade = GetEdgeFade(particle.Position, visibleBounds, drawBounds);
        var spawnFade = SmoothStep(0f, SpawnFadeEnd, particle.SpawnAge);
        var strengthAlpha = AlphaBaseStrength + _windStrength * AlphaWindStrength;

        return particle.Alpha * strengthAlpha * fadeIn * fadeOut * edgeFade * spawnFade;
    }

    private static float GetEdgeFade(Vector2 position, Box2 visibleBounds, Box2 drawBounds)
    {
        if (visibleBounds.Contains(position))
            return 1f;

        var outerDistance = MathF.Min(
            MathF.Min(position.X - drawBounds.Left, drawBounds.Right - position.X),
            MathF.Min(position.Y - drawBounds.Bottom, drawBounds.Top - position.Y));

        var innerDistance = MathF.Max(
            MathF.Max(visibleBounds.Left - position.X, position.X - visibleBounds.Right),
            MathF.Max(visibleBounds.Bottom - position.Y, position.Y - visibleBounds.Top));

        var fadeWidth = MathF.Max(outerDistance + innerDistance, MinimumFadeWidth);
        return SmoothStep(0f, fadeWidth, outerDistance);
    }

    private static Vector2[] GetCurvePoints(WindParticle particle)
    {
        var halfLength = particle.Length * 0.5f;
        var perpendicular = new Vector2(-particle.Direction.Y, particle.Direction.X);
        var start = particle.Position - particle.Direction * halfLength;
        var end = particle.Position + particle.Direction * halfLength;
        var control = particle.Position + particle.Direction * particle.CurveBias + perpendicular * particle.Curve;
        var points = new Vector2[CurvePointCount];

        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (points.Length - 1f);
            points[i] = QuadraticBezier(start, control, end, t);
        }

        return points;
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        var invT = 1f - t;
        return invT * invT * start + 2f * invT * t * control + t * t * end;
    }

    private float GetRibbonHalfWidth(WindParticle particle, float pointT)
    {
        var centerWeight = SmoothStep(0f, 1f, 1f - MathF.Abs(pointT - 0.5f) * 2f);
        centerWeight *= centerWeight;

        return (RibbonBaseHalfWidth +
                _windStrength * RibbonStrengthHalfWidth +
                centerWeight * RibbonCenterHalfWidth) *
               particle.WidthScale;
    }

    private void DrawRibbon(DrawingHandleWorld handle, Vector2[] curve, WindParticle particle, Color color)
    {
        var vertices = new Vector2[(curve.Length - 1) * VerticesPerSegment];
        var vertexIndex = 0;

        for (var i = 0; i < curve.Length - 1; i++)
        {
            var startT = i / (float) (curve.Length - 1);
            var endT = (i + 1) / (float) (curve.Length - 1);
            var startNormal = GetRibbonNormal(curve, i);
            var endNormal = GetRibbonNormal(curve, i + 1);
            var startHalfWidth = GetRibbonHalfWidth(particle, startT);
            var endHalfWidth = GetRibbonHalfWidth(particle, endT);
            var startLeft = curve[i] - startNormal * startHalfWidth;
            var startRight = curve[i] + startNormal * startHalfWidth;
            var endLeft = curve[i + 1] - endNormal * endHalfWidth;
            var endRight = curve[i + 1] + endNormal * endHalfWidth;

            vertices[vertexIndex++] = startLeft;
            vertices[vertexIndex++] = startRight;
            vertices[vertexIndex++] = endRight;
            vertices[vertexIndex++] = startLeft;
            vertices[vertexIndex++] = endRight;
            vertices[vertexIndex++] = endLeft;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, vertices, color);
    }

    private static Vector2 GetRibbonNormal(Vector2[] curve, int index)
    {
        var tangent = index switch
        {
            0 => curve[1] - curve[0],
            var last when last == curve.Length - 1 => curve[index] - curve[index - 1],
            _ => curve[index + 1] - curve[index - 1],
        };

        return tangent.LengthSquared() <= MinimumTangentLengthSquared
            ? Vector2.UnitY
            : Vector2.Normalize(new Vector2(-tangent.Y, tangent.X));
    }

    private float RandomRange(float min, float max)
    {
        return _random.NextFloat(min, max);
    }

    private static float NormalizeStrength(float strength)
    {
        return strength <= VisibleStrengthThreshold
            ? 0f
            : Math.Clamp(strength / StrengthScale, 0f, 1f);
    }

    private static float Approach(float current, float target, float frameTime, float speed)
    {
        return current + (target - current) * SmoothRatio(frameTime, speed);
    }

    private static float SmoothRatio(float frameTime, float speed)
    {
        return MathF.Min(1f, frameTime * speed);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (MathF.Abs(edge1 - edge0) < float.Epsilon)
            return value >= edge1 ? 1f : 0f;

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= FullRotation;
        }

        while (angle < -MathF.PI)
        {
            angle += FullRotation;
        }

        return angle;
    }

    private readonly record struct ParticleSpawnBounds(float DirectionExtent, float LateralExtent);

    private struct WindParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 CurveVelocity;
        public Vector2 Direction;
        public float Age;
        public float SpawnAge;
        public float Lifetime;
        public float Length;
        public float Curve;
        public float CurveBias;
        public float Alpha;
        public float WidthScale;
    }
}
