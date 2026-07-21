using System;
using System.Collections.Generic;
using Content.Shared.Imperial.Medieval.Ships.Hull;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;
using Content.Shared.Movement.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Content.Shared._RD.Weight.Systems;
using Content.Server.Imperial.Medieval.Ships.Sail;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Robust.Shared.Configuration;
using Content.Shared.Imperial.Medieval.Administration.Ships;

namespace Content.Server.Imperial.Medieval.Ships.ShipDrowning;

public sealed class ShipDrowningSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedShipHullSystem _shipHull = default!;
    [Dependency] private readonly RDWeightSystem _rdWeight = default!;
    [Dependency] private readonly SailSystem _sail = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private const float UpdateDelaySeconds = 1f;

    private readonly List<EntityUid> _gridChildren = new();
    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(UpdateDelaySeconds);
        SubscribeLocalEvent<ShipDrowningComponent, EntityTerminatingEvent>(OnShipTerminating);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        if (curTime < _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(UpdateDelaySeconds);

        var enumerator = EntityQueryEnumerator<ShipDrowningComponent, MapGridComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var drowning, out var mapGrid, out _))
        {
            var previousDrownLevel = drowning.DrownLevel;
            var previousDrownMaxLevel = drowning.DrownMaxLevel;
            var totalTiles = 0;
            var floodContribution = 0;
            var allTiles = _map.GetAllTilesEnumerator(uid, mapGrid);

            while (allTiles.MoveNext(out var tile))
            {
                totalTiles++;
                floodContribution += _shipHull.GetFloodContribution(tile.Value.Tile.TypeId);
            }

            if (totalTiles == 0)
                continue;

            var weight = _rdWeight.GetTotalOnGrid(uid);
            float maxWeight = ShipWeightHelper.GetMaxWeight(uid, mapGrid, _map, EntityManager, _cfg);

            if (weight > maxWeight * 3)
                drowning.DrownLevel += _cfg.GetCVar(ShipsCCVars.OverloadDrownRate) * frameTime;

            drowning.DrownMaxLevel = totalTiles * drowning.MaxFloodPerTile;
            drowning.DrownLevel += floodContribution * drowning.FloodPerDamageStage;

            if (drowning.DrownLevel < drowning.DrownMaxLevel * 0.5f)
                drowning.DrownLevel -= drowning.PassiveDrainPerTick;
            else
                drowning.DrownLevel += drowning.PassiveRisePerTick;

            drowning.DrownLevel = Math.Max(0, drowning.DrownLevel);

            if (drowning.DrownLevel >= drowning.DrownMaxLevel)
            {
                SinkShip(uid);
                continue;
            }

            if (drowning.DrownLevel != previousDrownLevel || Math.Abs(drowning.DrownMaxLevel - previousDrownMaxLevel) > float.Epsilon)
                Dirty(uid, drowning);
        }
    }

    private void SinkShip(EntityUid ship)
    {
        EntityManager.QueueDeleteEntity(ship);
    }

    private void OnShipTerminating(EntityUid uid, ShipDrowningComponent component, ref EntityTerminatingEvent args)
    {
        RescueGridChildrenToMap(uid);
    }

    private void RescueGridChildrenToMap(EntityUid uid)
    {
        var shipXform = Transform(uid);
        if (!_map.TryGetMap(shipXform.MapID, out var mapUid))
            return;

        _gridChildren.Clear();
        var entityEnumerator = EntityQueryEnumerator<TransformComponent>();
        while (entityEnumerator.MoveNext(out var child, out var childXform))
        {
            if (child == uid || TerminatingOrDeleted(child))
                continue;

            if (childXform.ParentUid != uid)
                continue;

            _gridChildren.Add(child);
        }

        foreach (var child in _gridChildren)
        {
            var childXform = Transform(child);
            var mapCoordinates = _transform.GetMapCoordinates(child, childXform);
            var worldRotation = _transform.GetWorldRotation(child);
            var traversal = childXform.GridTraversal;
            childXform.GridTraversal = false;
            _transform.SetCoordinates(child, childXform, new EntityCoordinates(mapUid.Value, mapCoordinates.Position), rotation: worldRotation);
            childXform.GridTraversal = traversal;
        }

        UpdateMoverRelativeEntities(uid, mapUid.Value);
    }

    private void UpdateMoverRelativeEntities(EntityUid gridUid, EntityUid mapUid)
    {
        var oldRelativeRotation = Angle.Zero;
        if (TryComp<TransformComponent>(gridUid, out var oldRelativeXform))
            oldRelativeRotation = _transform.GetWorldRotation(oldRelativeXform);

        var newRelativeRotation = Angle.Zero;
        if (TryComp<TransformComponent>(mapUid, out var newRelativeXform))
            newRelativeRotation = _transform.GetWorldRotation(newRelativeXform);

        var diff = newRelativeRotation - oldRelativeRotation;
        var moverEnumerator = EntityQueryEnumerator<InputMoverComponent>();
        while (moverEnumerator.MoveNext(out var moverUid, out var mover))
        {
            if (mover.RelativeEntity != gridUid)
                continue;

            mover.TargetRelativeRotation -= diff;
            mover.RelativeRotation -= diff;
            mover.RelativeEntity = mapUid;
            mover.LerpTarget = TimeSpan.Zero;
            Dirty(moverUid, mover);
        }
    }
}
