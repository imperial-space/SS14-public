using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Imperial.ZLevels;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Воздух между этажами (zAirIn/zAirOut SS13):
/// клетка открытого пространства делит газ с клеткой прямо под ней — под ней пустота или космос, и газ уходит
/// в космос; стена или другая герметичная клетка — обмена нет;
/// межэтажная вентиляция делит газ своей клетки с клеткой связанной вентиляции этажом ниже.
/// </summary>
public sealed class ZAtmosSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>Примерно такт атмоса.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _next;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + Interval;

        var open = EntityQueryEnumerator<ZOpenSpaceComponent, TransformComponent>();
        while (open.MoveNext(out var uid, out _, out _))
        {
            var here = _z.SnapToTile(_xform.GetMapCoordinates(uid));
            if (_z.GetStep(here, false) is { } below)
                Exchange(here, below);
        }

        var vents = EntityQueryEnumerator<ZLadderComponent, TransformComponent>();
        while (vents.MoveNext(out var uid, out var vent, out var xform))
        {
            if (!vent.PassGas || !xform.Anchored || vent.Down is not { } down || TerminatingOrDeleted(down))
                continue;
            Exchange(_xform.GetMapCoordinates(uid), _xform.GetMapCoordinates(down));
        }
    }

    /// <summary>Уравнивает газ двух клеток (на сетках или в космосе карты); герметичная клетка не участвует.</summary>
    private void Exchange(MapCoordinates a, MapCoordinates b)
    {
        if (GetMixture(a) is not { } top || GetMixture(b) is not { } bottom)
            return;
        if (top.Mix.Immutable && bottom.Mix.Immutable)
            return;
        if (MathF.Abs(top.Mix.TotalMoles - bottom.Mix.TotalMoles) < 0.05f && MathF.Abs(top.Mix.Temperature - bottom.Mix.Temperature) < 0.5f)
            return;

        if (bottom.Mix.Immutable)
            SetKeepVolume(top.Mix, bottom.Mix);
        else if (top.Mix.Immutable)
            SetKeepVolume(bottom.Mix, top.Mix);
        else
        {
            var total = new GasMixture(top.Mix.Volume + bottom.Mix.Volume);
            _atmos.Merge(total, top.Mix);
            _atmos.Merge(total, bottom.Mix);
            var topPart = total.RemoveRatio(top.Mix.Volume / total.Volume);
            SetKeepVolume(top.Mix, topPart);
            SetKeepVolume(bottom.Mix, total);
        }

        // Разбудить обе клетки, чтобы газ пошёл дальше по этажам.
        _atmos.GetTileMixture(top.Grid, top.Map, top.Indices, true);
        _atmos.GetTileMixture(bottom.Grid, bottom.Map, bottom.Indices, true);
    }

    private (GasMixture Mix, EntityUid? Grid, EntityUid Map, Vector2i Indices)? GetMixture(MapCoordinates coords)
    {
        if (!_map.TryGetMap(coords.MapId, out var mapUid))
            return null;
        EntityUid? grid = null;
        var indices = Vector2i.Zero;
        if (_mapManager.TryFindGridAt(coords, out var gridUid, out var gridComp))
        {
            indices = _map.TileIndicesFor(gridUid, gridComp, coords);
            if (_atmos.IsTileAirBlocked(gridUid, indices, mapGridComp: gridComp))
                return null;
            grid = gridUid;
        }
        return _atmos.GetTileMixture(grid, mapUid.Value, indices) is { } mix ? (mix, grid, mapUid.Value, indices) : null;
    }

    private static void SetKeepVolume(GasMixture target, GasMixture source)
    {
        var volume = target.Volume;
        target.CopyFrom(source);
        target.Volume = volume;
    }
}
