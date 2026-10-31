using System.Numerics;
using Content.Client.Light;
using Content.Shared.Imperial.ZLevels;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Imperial.ZLevels;

/// <summary>
/// Свет между этажами: там, где сквозь верхний этаж виден нижний, освещение верхнего не должно его затемнять —
/// нижний уже отрендерен со своими лампами. В карту освещения кадра в этих клетках пишется полный свет.
/// </summary>
public sealed class ZLevelLightOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    private List<Entity<MapGridComponent>> _grids = new();

    public ZLevelLightOverlay()
    {
        IoCManager.InjectDependencies(this);
        // После потолков и светящихся тайлов, до размытия — края дыр получаются мягкими.
        ZIndex = TileEmissionOverlay.ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye || !TryGetBelow(args.MapId, out var belowMap))
            return;

        var lightOverlay = _overlays.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightOverlay.EnlargedBounds;
        var target = lightOverlay.GetCachedForViewport(args.Viewport).EnlargedLightTarget;
        var viewport = args.Viewport;
        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);
        var gap = ZLevelOverlay.Gap(_ents, args.MapId);
        var lowerEye = ZLevelOverlay.DepthEye(eye, belowMap, gap);
        var box = bounds.CalcBoundingBox().Enlarged(2f);
        var upperMap = args.MapId;
        var eyePos = eye.Position.Position;
        var handle = args.WorldHandle;

        var maps = _ents.System<SharedMapSystem>();
        var xforms = _ents.System<SharedTransformSystem>();

        _grids.Clear();
        _mapManager.FindGridsIntersecting(belowMap, box, ref _grids);
        if (_grids.Count == 0)
            return;

        handle.RenderInRenderTarget(target, () =>
        {
            var worldToTarget = target.GetWorldToLocalMatrix(lowerEye, scale);
            foreach (var grid in _grids)
            {
                var tileSize = grid.Comp.TileSize;
                handle.SetTransform(xforms.GetWorldMatrix(grid.Owner) * worldToTarget);
                foreach (var tile in maps.GetTilesIntersecting(grid.Owner, grid.Comp, box))
                {
                    // Где эта клетка видна на экране (нижний этаж чуть мельче к центру) — там и проверяем верхний пол.
                    var center = maps.GridTileToWorldPos(grid.Owner, grid.Comp, tile.GridIndices);
                    var apparent = eyePos + (center - eyePos) * MathF.Pow(ZLevelOverlay.DepthScale, gap);
                    if (IsCovered(new MapCoordinates(apparent, upperMap)))
                        continue;
                    var origin = (Vector2) tile.GridIndices * tileSize;
                    handle.DrawRect(new Box2(origin, origin + new Vector2(tileSize, tileSize)), Color.White);
                }
            }
        }, null);
    }

    /// <summary>Над клеткой нижнего этажа непрозрачный пол — её не видно, свет не трогаем.</summary>
    private bool IsCovered(MapCoordinates coords)
    {
        if (!_mapManager.TryFindGridAt(coords, out var grid, out var gridComp))
            return false;
        var maps = _ents.System<SharedMapSystem>();
        var tile = maps.GetTileRef(grid, gridComp, coords).Tile;
        return !tile.IsEmpty && !ZLevelTiles.IsTransparent(_tileDefs, tile);
    }

    private bool TryGetBelow(MapId map, out MapId below)
    {
        return ZLevelOverlay.ResolveBelow(_ents, map, out below, out _);
    }
}
