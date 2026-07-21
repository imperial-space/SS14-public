using System;
using Content.Server.Imperial.Medieval.Ships.Sea.Generation;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.ShipTeleporting;

public sealed class ShipTeleportSystem : EntitySystem
{
    private const int SeaMatrixSize = 5;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
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
            var entities = new HashSet<Entity<MapGridComponent>>();
            _lookup.GetEntitiesOnMap(_transform.GetMapId(sea), entities);
            foreach (var shipComp in entities)
            {
                var ship = shipComp.Owner;
                var coords = _transform.GetMapCoordinates(ship);
                var tpDist = _cfg.GetCVar(ShipsCCVars.MapScale) + _cfg.GetCVar(ShipsCCVars.TeleportRange);
                if (Math.Abs(coords.X) <= tpDist && Math.Abs(coords.Y) <= tpDist)
                    continue;

                TeleportShip(ship, coords);
            }
        }
    }

    private void TeleportShip(EntityUid ship, MapCoordinates coords)
    {
        var mapScale = _cfg.GetCVar(ShipsCCVars.MapScale);
        var tpRange = _cfg.GetCVar(ShipsCCVars.TeleportRange);
        var tpDist = mapScale + tpRange;
        var newcoords = coords.Position;

        foreach (var seasGenerationState in EntityManager.EntityQuery<SeasGenerationStateComponent>())
        {
            var seematrix = seasGenerationState.SeaMatrix;
            if (seematrix is null)
                continue;

            var seamap = seematrix.FoundSell(coords.MapId, seematrix);
            if (seamap is null)
                continue;

            var (x, y) = seamap.Value;

            if (Math.Abs(newcoords.X) > tpDist)
            {
                if (newcoords.X > 0)
                {
                    x += 1;
                    newcoords.X = -tpDist;
                }
                else
                {
                    x -= 1;
                    newcoords.X = tpDist;
                }
            }

            if (Math.Abs(newcoords.Y) > tpDist)
            {
                if (newcoords.Y > 0)
                {
                    y += 1;
                    newcoords.Y = -tpDist;
                }
                else
                {
                    y -= 1;
                    newcoords.Y = tpDist;
                }
            }

            if (x < 0 || x >= SeaMatrixSize || y < 0 || y >= SeaMatrixSize)
            {
                ApplyDrowningPenalty(ship, coords);
                continue;
            }

            var mapId = seematrix.GetCell(x, y).SeaId;
            if (mapId == new MapId(-1))
            {
                ApplyDrowningPenalty(ship, coords);
                continue;
            }

            var nmapcoords = new MapCoordinates(newcoords, mapId);
            _transform.SetMapCoordinates(ship, nmapcoords);
            break;
        }
    }

    private void ApplyDrowningPenalty(EntityUid ship, MapCoordinates coords)
    {
        EnsureComp<ShipDrowningComponent>(ship, out var comp);
        var previousLevel = comp.DrownLevel;
        // Disabled here
        // comp.DrownLevel += (int) Math.Abs(coords.Position.X) + (int) Math.Abs(coords.Position.Y);
        if (comp.DrownLevel != previousLevel)
            Dirty(ship, comp);
    }
}
