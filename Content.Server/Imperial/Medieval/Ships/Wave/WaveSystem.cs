using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Hull;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Content.Shared.Tiles;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Medieval.Ships.Wave;

public sealed class WaveSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedShipHullSystem _shipHull = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private readonly List<Vector2i> _nearbyTiles = new();
    private readonly HashSet<EntityUid> _tileContents = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WaveComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WaveComponent, StartCollideEvent>(OnCollide);
    }

    private void OnStartup(EntityUid uid, WaveComponent component, ComponentStartup args)
    {
        if (!TryGetShipGridAt(_transform.GetMapCoordinates(uid), out _, out _))
            return;

        EntityManager.QueueDeleteEntity(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<WaveComponent>();
        while (enumerator.MoveNext(out var uid, out var component))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var collisionPos = _transform.GetMapCoordinates(uid);
            if (!_mapManager.TryFindGridAt(collisionPos, out var gridUid, out var mapGridComp))
                continue;

            HandleWaveImpact(uid, component, gridUid, mapGridComp, collisionPos);
        }
    }

    private void OnCollide(EntityUid uid, WaveComponent component, ref StartCollideEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.OtherEntity))
            return;

        if (!TryResolveCollisionGrid(args.OtherEntity, out var targetEntity, out var mapGridComp))
        {
            if (component.DeleteOnCollide)
                EntityManager.DeleteEntity(args.OurEntity);

            return;
        }

        var collisionPos = _transform.GetMapCoordinates(args.OurEntity);
        HandleWaveImpact(args.OurEntity, component, targetEntity, mapGridComp, collisionPos);
    }

    private bool TryResolveCollisionGrid(EntityUid targetEntity, out EntityUid gridUid, out MapGridComponent mapGridComp)
    {
        if (TryComp<MapGridComponent>(targetEntity, out var directGridComp) && directGridComp != null)
        {
            mapGridComp = directGridComp;
            gridUid = targetEntity;
            return true;
        }

        var resolvedGridUid = _transform.GetGrid(targetEntity);
        if (resolvedGridUid.HasValue &&
            TryComp<MapGridComponent>(resolvedGridUid.Value, out var parentGridComp) &&
            parentGridComp != null)
        {
            mapGridComp = parentGridComp;
            gridUid = resolvedGridUid.Value;
            return true;
        }

        gridUid = EntityUid.Invalid;
        mapGridComp = default!;
        return false;
    }

    private bool TryGetShipGridAt(MapCoordinates coords, out EntityUid gridUid, out MapGridComponent mapGridComp)
    {
        if (_mapManager.TryFindGridAt(coords, out gridUid, out var foundGridComp) &&
            HasComp<ShipDrowningComponent>(gridUid))
        {
            mapGridComp = foundGridComp;
            return true;
        }

        gridUid = EntityUid.Invalid;
        mapGridComp = default!;
        return false;
    }

    private void HandleWaveImpact(
        EntityUid wave,
        WaveComponent component,
        EntityUid targetEntity,
        MapGridComponent mapGridComp,
        MapCoordinates collisionPos)
    {
        if (TerminatingOrDeleted(wave) || TerminatingOrDeleted(targetEntity))
            return;

        if (component.HitList.Contains(targetEntity))
            return;

        if (!HasComp<ShipDrowningComponent>(targetEntity))
        {
            if (component.DeleteOnCollide)
                DeleteWaveOnGridImpact(wave, component, collisionPos);

            return;
        }

        if (_cfg.GetCVar(ShipsCCVars.WaveMinToBreakLevel) > _cfg.GetCVar(ShipsCCVars.StormLevel))
        {
            if (component.DeleteOnCollide)
                DeleteWaveOnGridImpact(wave, component, collisionPos);

            return;
        }

        var grid = new Entity<MapGridComponent>(targetEntity, mapGridComp);
        var centerTilePos = _map.MapToGrid(grid, collisionPos);
        var radiusTiles = _cfg.GetCVar(ShipsCCVars.WaveRadiusTiles) + _cfg.GetCVar(ShipsCCVars.StormLevel);
        var radiusLimit = (int) MathF.Ceiling(radiusTiles);
        var radiusSquared = radiusTiles * radiusTiles;

        _nearbyTiles.Clear();

        for (var dx = -radiusLimit; dx <= radiusLimit; dx++)
        {
            for (var dy = -radiusLimit; dy <= radiusLimit; dy++)
            {
                var tileCoordinates = centerTilePos.Offset(new Vector2(dx, dy));
                if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tileCoordinates, out var tile))
                    continue;

                if (tile.Tile.IsEmpty || !_shipHull.TryGetDamageStage(tile.Tile.TypeId, out _))
                    continue;

                _tileContents.Clear();
                _lookup.GetEntitiesInTile(tile, _tileContents);

                var blockedByWall = false;
                foreach (var tileEntity in _tileContents)
                {
                    if (!_tags.HasTag(tileEntity, "Wall"))
                        continue;

                    blockedByWall = true;
                    break;
                }

                if (blockedByWall)
                    continue;

                var delta = tileCoordinates.Position - centerTilePos.Position;
                if (delta.LengthSquared() <= radiusSquared)
                    _nearbyTiles.Add(tile.GridIndices);
            }
        }

        if (_nearbyTiles.Count == 0)
        {
            if (component.DeleteOnCollide)
                DeleteWaveOnGridImpact(wave, component, collisionPos);

            return;
        }

        var maxBreakCount = Math.Max(0, _cfg.GetCVar(ShipsCCVars.WaveMaxBreakCount));
        if (maxBreakCount > 0)
        {
            _random.Shuffle(_nearbyTiles);

            var tilesToReplace = Math.Min(_random.Next(1, maxBreakCount + 1), _nearbyTiles.Count);
            for (var i = 0; i < tilesToReplace; i++)
            {
                var tilePos = _nearbyTiles[i];
                if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tilePos, out var tile) || tile.Tile.IsEmpty)
                    continue;

                if (!_shipHull.TryGetNextDamageTile(tile.Tile.TypeId, out var damagedTileType))
                    continue;

                _map.SetTile(grid.Owner, grid, tilePos, _shipHull.WithTileType(tile.Tile, damagedTileType));
            }
        }

        if (!TerminatingOrDeleted(targetEntity))
            component.HitList.Add(targetEntity);

        if (component.DeleteOnCollide)
            DeleteWaveOnGridImpact(wave, component, collisionPos);
    }

    private void DeleteWaveOnGridImpact(EntityUid wave, WaveComponent component, MapCoordinates collisionPos)
    {
        RepulseEntitiesFromWaveImpact(wave, component, collisionPos);
        EntityManager.DeleteEntity(wave);
    }

    private void RepulseEntitiesFromWaveImpact(EntityUid wave, WaveComponent component, MapCoordinates collisionPos)
    {
        var stormLevel = MathF.Max(0f, _cfg.GetCVar(ShipsCCVars.StormLevel));
        var range = component.RepulseRangePerStormLevel * stormLevel;
        var distance = component.RepulseDistancePerStormLevel * stormLevel;
        if (range <= 0f || distance <= 0f)
            return;

        var repulseTargets = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(
            collisionPos.MapId,
            collisionPos.Position,
            range,
            repulseTargets,
            flags: LookupFlags.Dynamic | LookupFlags.Sundries);

        foreach (var target in repulseTargets)
        {
            if (target == wave ||
                TerminatingOrDeleted(target) ||
                HasComp<MapGridComponent>(target) ||
                HasComp<MapComponent>(target) ||
                HasComp<UndrowableComponent>(target))
            {
                continue;
            }

            var targetPos = _transform.GetMapCoordinates(target);
            if (targetPos.MapId != collisionPos.MapId)
                continue;

            var direction = targetPos.Position - collisionPos.Position;
            var directionLength = direction.Length();
            if (directionLength <= 0f)
                continue;

            _throwing.TryThrow(
                target,
                direction / directionLength * distance,
                baseThrowSpeed: MathF.Max(distance, 0.1f),
                recoil: false,
                compensateFriction: true);
        }
    }

    public EntityUid? SpawnWave(MapCoordinates coords, Vector2 velocity = default, bool deleteOnCollide = true, float lifetime = 60)
    {
        if (!_map.TryGetMap(coords.MapId, out _))
            return null;

        if (TryGetShipGridAt(coords, out _, out _))
            return null;

        var wave = Spawn("WaveLarge", coords);
        if (TerminatingOrDeleted(wave))
            return null;

        var waveComponent = EnsureComp<WaveComponent>(wave);
        waveComponent.DeleteOnCollide = deleteOnCollide;

        if (TryComp<PhysicsComponent>(wave, out var body))
        {
            _physics.WakeBody(wave, body: body);
            _physics.ApplyLinearImpulse(wave, velocity * body.Mass, body: body);
        }

        RemComp<TimedDespawnComponent>(wave);
        RemComp<MedievalTimedDespawnComponent>(wave);
        if (lifetime <= 0)
            return wave;

        var despawnComponent = EnsureComp<MedievalTimedDespawnComponent>(wave);
        despawnComponent.Lifetime = lifetime;
        despawnComponent.OriginalLifeTime = lifetime;
        return wave;
    }
}
