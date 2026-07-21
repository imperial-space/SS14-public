using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Medieval.Ships;
public static class ShipWeightHelper
{
    /// <summary>
    /// Calculate max weight for ship (grid). Requires ShipWeightComponent on ship (grid)
    /// </summary>
    /// <param name="gridUid"></param>
    /// <param name="mapGrid"></param>
    /// <param name="mapSystem"></param>
    /// <param name="entityManager"></param>
    /// <param name="cfg"></param>
    /// <returns></returns>
    public static float GetMaxWeight(EntityUid gridUid, MapGridComponent mapGrid, SharedMapSystem mapSystem, EntityManager entityManager, IConfigurationManager cfg)
    {
        var totalTiles = 0;
        var allTiles = mapSystem.GetAllTilesEnumerator(gridUid, mapGrid);

        var overloadCeilPerTile = cfg.GetCVar(ShipsCCVars.OverloadCeilPerTile);
        if (entityManager.TryGetComponent<ShipWeightComponent>(gridUid, out var shipWeightComponent))
            overloadCeilPerTile = shipWeightComponent.OverloadCeilPerTile;

        while (allTiles.MoveNext(out _))
            totalTiles++;

        var maxWeight = totalTiles * overloadCeilPerTile;
        return maxWeight;
    }

    public static float GetMaxWeight(EntityUid gridUid, MapGridComponent mapGrid, SharedMapSystem mapSystem, EntityManager entityManager)
    {
        var totalTiles = 0;
        var allTiles = mapSystem.GetAllTilesEnumerator(gridUid, mapGrid);

        var overloadCeilPerTile = 0f;
        if (entityManager.TryGetComponent<ShipWeightComponent>(gridUid, out var shipWeightComponent))
            overloadCeilPerTile = shipWeightComponent.OverloadCeilPerTile;

        while (allTiles.MoveNext(out _))
            totalTiles++;

        var maxWeight = totalTiles * overloadCeilPerTile;
        return maxWeight;
    }
}
