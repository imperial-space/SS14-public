using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Buckle;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.SyndieBattle;

public sealed partial class SyndieBattleRuleSystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly TurfSystem _turfSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedBuckleSystem _buckleSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    private void ScatterCombatant(EntityUid player, EntityUid? station, SyndieBattleRuleComponent rule)
    {
        if (station is not { } stationUid ||
            !TryComp<StationDataComponent>(stationUid, out var stationData) ||
            _stationSystem.GetLargestGrid((stationUid, stationData)) is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            Transform(gridUid).MapUid is not { } map)
            return;

        var bounds = grid.LocalAABB;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        EntityCoordinates? spawn = null;
        var nearby = new HashSet<Entity<MobStateComponent>>();
        for (var attempt = 0; attempt < rule.SpawnPositionAttempts; attempt++)
        {
            var point = new EntityCoordinates(gridUid, _random.NextVector2Box(bounds.Left, bounds.Bottom, bounds.Right, bounds.Top));
            var tile = _mapSystem.LocalToTile(gridUid, grid, point);
            if (!_mapSystem.TryGetTileRef(gridUid, grid, tile, out var turf) || _turfSystem.IsSpace(turf) ||
                _turfSystem.IsTileBlocked(gridUid, tile, CollisionGroup.MobMask, grid) ||
                !_atmosphereSystem.IsTileMixtureProbablySafe(gridUid, map, tile))
            {
                continue;
            }

            var coords = _mapSystem.GridTileToLocal(gridUid, grid, tile);
            spawn ??= coords;
            nearby.Clear();
            _lookupSystem.GetEntitiesInRange(coords, rule.SpawnSeparation, nearby, LookupFlags.Dynamic);
            if (nearby.Any(mob => mob.Owner != player && !_mobStateSystem.IsDead(mob, mob.Comp)))
                continue;

            spawn = coords;
            break;
        }

        if (spawn is not { } destination)
        {
            Log.Warning($"Could not find a safe SyndieBattle spawn for {ToPrettyString(player)}.");
            return;
        }

        _buckleSystem.Unbuckle(player, null);
        if (TerminatingOrDeleted(player))
            return;

        _containerSystem.TryRemoveFromContainer(player, force: true);
        if (TerminatingOrDeleted(player) || TerminatingOrDeleted(gridUid))
            return;

        _transformSystem.SetCoordinates(player, destination);
    }
}
