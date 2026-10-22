using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Cult;

/// <summary>holywater/expose_turf(): святая вода освящает пол (turf/Bless) и смывает руны от 10u.</summary>
public sealed partial class CultStructureSystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private const string HolyWater = "Holywater";
    private const string BlessingProto = "CultBlessing";

    private TimeSpan _nextHolyScan;
    private readonly HashSet<EntityUid> _blessedPuddles = new();

    private void UpdateHolyWater(TimeSpan now)
    {
        if (now < _nextHolyScan)
            return;
        _nextHolyScan = now + TimeSpan.FromSeconds(1);

        _blessedPuddles.RemoveWhere(p => TerminatingOrDeleted(p));
        var query = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var xform))
        {
            if (_blessedPuddles.Contains(uid))
                continue;
            if (!_solutions.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                continue;
            var holy = solution.GetTotalPrototypeQuantity(HolyWater);
            if (holy <= 0)
                continue;

            _blessedPuddles.Add(uid);
            var coords = SnapTile(xform.Coordinates);
            if (holy >= 10)
            {
                foreach (var rune in _lookup.GetEntitiesInRange<CultRuneComponent>(coords, 0.45f))
                    QueueDel(rune);
            }
            Bless(coords);
        }
    }

    /// <summary>turf/Bless().</summary>
    public void Bless(EntityCoordinates coords)
    {
        coords = SnapTile(coords);
        foreach (var _ in _lookup.GetEntitiesInRange<CultBlessingComponent>(coords, 0.45f))
            return;
        Spawn(BlessingProto, coords);
    }

    private EntityCoordinates SnapTile(EntityCoordinates coords)
    {
        var grid = _transform.GetGrid(coords);
        if (grid == null || !TryComp<MapGridComponent>(grid, out var gridComp))
            return coords;
        return _map.GridTileToLocal(grid.Value, gridComp, _map.TileIndicesFor(grid.Value, gridComp, coords));
    }
}
