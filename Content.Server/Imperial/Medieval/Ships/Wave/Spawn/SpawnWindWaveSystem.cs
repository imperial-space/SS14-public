using System;
using System.Numerics;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Wave.Spawn;

public sealed class SpawnWindWaveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly WaveSystem _wave = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    private TimeSpan _nextCheckTime;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime <= _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WaveDelay));
        foreach (var seaComponent in EntityManager.EntityQuery<SeaComponent>())
        {
            if (seaComponent.Disabled)
                continue;
            var sea = seaComponent.Owner;
            var seaMapId = _transform.GetMapId(sea);
            var ships = new HashSet<Entity<ShipDrowningComponent>>();
            _lookup.GetEntitiesOnMap(seaMapId, ships);

            foreach (var shipComp in ships)
            {
                if (shipComp.Comp.DisableWavesTime is { } disableTime && disableTime <= _timing.CurTime)
                    continue;

                var ship = shipComp.Owner;
                if (!TryComp<MapGridComponent>(ship, out var grid))
                    continue;

                var maxWaves = Math.Max(0, (int) MathF.Ceiling(_cfg.GetCVar(ShipsCCVars.StormLevel)));
                var waveCount = _random.Next(0, maxWaves + 1);
                var shipCenter = _transform.ToMapCoordinates(new EntityCoordinates(ship, grid.LocalAABB.Center));
                var shipRadius = grid.LocalAABB.Size.Length() * 0.5f;

                for (var i = 0; i < waveCount; i++)
                {
                    var waveOffset = GenerateWave();
                    var offsetLength = waveOffset.Length();
                    if (offsetLength <= 0f)
                        continue;

                    var windDirection = waveOffset / offsetLength;
                    var spawnDirection = -windDirection;
                    var spawnDistance = shipRadius + _cfg.GetCVar(ShipsCCVars.WaveMinSpawnDistance) + offsetLength;
                    if (!TryFindValidSpawnPosition(seaMapId, shipCenter.Position, spawnDirection, spawnDistance, out var waveCoords))
                        continue;

                    var wavePosition = waveCoords.Position;
                    var velocityDirection = shipCenter.Position - wavePosition;
                    var velocity = velocityDirection.Normalized() * _cfg.GetCVar(ShipsCCVars.WaveForce);
                    _wave.SpawnWave(waveCoords, velocity);
                }
            }
        }
    }

    private bool TryFindValidSpawnPosition(MapId mapId, Vector2 shipCenter, Vector2 direction, float initialDistance, out MapCoordinates coords)
    {
        var distance = initialDistance;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var wavePosition = shipCenter + direction * distance;
            if (!_mapManager.TryFindGridAt(mapId, wavePosition, out _, out _))
            {
                coords = new MapCoordinates(wavePosition, mapId);
                return true;
            }

            distance += 1f;
        }

        coords = default;
        return false;
    }

    private Vector2 GenerateWave()
    {
        var radius = _cfg.GetCVar(ShipsCCVars.WaveSpawnRange);
        var targetAngle = Angle.FromDegrees(_cfg.GetCVar(ShipsCCVars.WindRotation));
        var halfAngle = _cfg.GetCVar(ShipsCCVars.WaveSpawnAngle) * _cfg.GetCVar(ShipsCCVars.StormLevel);

        var rho = radius * MathF.Sqrt(_random.NextFloat());
        var angleOffset = Angle.FromDegrees(_random.NextFloat(-halfAngle, halfAngle));
        var direction = (targetAngle + angleOffset).ToWorldVec();
        return direction * rho;
    }
}
