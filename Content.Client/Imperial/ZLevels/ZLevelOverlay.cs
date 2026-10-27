using System.Numerics;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.ZLevels;

/// <summary>
/// Этаж ниже сквозь пустоту, открытое пространство, стеклянный пол и люки трапов.
/// Нижняя карта рендерится отдельным вьюпортом с тем же глазом (этажи совмещены по мировым координатам) и
/// накладывается по маске. Маска строится по тайлам в координатах их сеток, поэтому повёрнутые станции совпадают
/// клетка в клетку: сначала закрашивается пол нижнего этажа, потом из неё вырезается непрозрачный пол верхнего.
/// </summary>
public sealed class ZLevelOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> OverwriteShader = "ZLevelOverwrite";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";

    /// <summary>Каждый этаж вниз чуть темнее — глубина читается, а соседний этаж не кажется далёким.</summary>
    private static readonly Color DepthTint = new(0.88f, 0.88f, 0.92f);

    /// <summary>Перспектива: каждый этаж вниз чуть мельче (к центру экрана), чем глубже — тем дальше.</summary>
    internal const float DepthScale = 0.95f;

    /// <summary>Стекло поверх вида вниз: только намёк на рамку и блик, чтобы сквозь него было видно и пол, и существ.</summary>
    private static readonly Color GlassTint = Color.White.WithAlpha(0.35f);

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    /// <summary>Вьюпорт этажа ниже и (для вложенного рендера) ещё одного ниже.</summary>
    private IClydeViewport? _lower;
    private IClydeViewport? _nestedLower;
    private IRenderTexture? _stencil;
    private IRenderTexture? _nestedStencil;
    private List<Entity<MapGridComponent>> _grids = new();

    public ZLevelOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = -10;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // Рендер этажа ниже идёт через этот же оверлей и рисует под собой ещё один этаж; глубже — нет.
        if (args.Viewport == _nestedLower)
            return;
        // Взгляд вниз: основной кадр закрыт нижним этажом целиком, этаж под ним рисуется уже в его вьюпорте.
        // Взгляд вверх: свой этаж остаётся основным кадром (со своим полем зрения), а верхний вьюпорт этажей
        // под собой не рисует — сквозь его дыры виден основной кадр.
        if (_ents.System<ZLevelSystem>().TryGetLookedMap(out var looked, out var lookingUp))
        {
            if (lookingUp && args.Viewport == _overlays.GetOverlay<ZLevelLookOverlay>().View)
                return;
            if (!lookingUp && looked != args.MapId && args.Viewport.Eye == _eyeManager.CurrentEye)
                return;
        }
        var nested = args.Viewport == _lower;

        if (args.Viewport.Eye is not { } eye || !TryGetBelow(args.MapId, out var belowMap))
        {
            Pause(nested);
            return;
        }

        var size = args.Viewport.Size;
        ref var lower = ref nested ? ref _nestedLower : ref _lower;
        if (lower == null || lower.Size != size)
        {
            lower?.Dispose();
            lower = _clyde.CreateViewport(size, nested ? "zlevel-below-2" : "zlevel-below");
            lower.ClearColor = Color.Black;
        }
        // Без этого при масштабировании рендера (render scale > 1) нижний этаж рисовался вдвое мельче — «далеко».
        lower.RenderScale = args.Viewport.RenderScale;

        var target = args.Viewport.RenderTarget;
        ref var stencil = ref nested ? ref _nestedStencil : ref _stencil;
        if (stencil?.Texture.Size != target.Size)
        {
            stencil?.Dispose();
            stencil = _clyde.CreateRenderTarget(target.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "zlevel-stencil");
        }

        var handle = args.WorldHandle;
        var lightScale = target.Size / (Vector2) args.Viewport.Size;
        var scale = args.Viewport.RenderScale / (Vector2.One / lightScale);
        var st = stencil;
        var box = args.WorldAABB.Enlarged(2f);
        var map = args.MapId;
        var below2 = TryGetBelow(belowMap, out var b2) ? b2 : MapId.Nullspace;
        var any = false;
        var lowerEye = DepthEye(eye, belowMap);
        // Нижние этажи мельче — их пол может выглядывать из-за края экрана.
        var lowBox = box.Enlarged(box.Size.Length() * (1f / (DepthScale * DepthScale) - 1f));

        handle.RenderInRenderTarget(st, () =>
        {
            var worldToTarget = st.GetWorldToLocalMatrix(eye, scale);

            // 1. Пол нижнего этажа (и этажа под ним — его видно сквозь пустоту нижнего), каждый в своей перспективе.
            handle.UseShader(_proto.Index(UnshadedShader).Instance());
            any |= DrawFloor(handle, belowMap, lowBox, st.GetWorldToLocalMatrix(lowerEye, scale), Color.White, false);
            if (below2 != MapId.Nullspace)
                any |= DrawFloor(handle, below2, lowBox, st.GetWorldToLocalMatrix(DepthEye(lowerEye, below2), scale), Color.White, false);

            // 2. Непрозрачный пол верхнего этажа закрывает вид вниз (перезапись в прозрачное).
            handle.UseShader(_proto.Index(OverwriteShader).Instance());
            DrawFloor(handle, map, box, worldToTarget, Color.Transparent, true);

            // 3. ladder_hole: под трапом, ведущим вниз, пол прозрачен.
            handle.UseShader(_proto.Index(UnshadedShader).Instance());
            DrawLadderHoles(handle, map, box, worldToTarget);
        }, Color.Transparent);

        if (!any)
        {
            Pause(nested);
            return;
        }

        lower.Eye = lowerEye;
        lower.AutomaticRender = true;

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(_proto.Index(StencilMaskShader).Instance());
        handle.DrawTextureRect(st.Texture, args.WorldBounds);
        handle.UseShader(_proto.Index(StencilEqualDrawShader).Instance());
        handle.DrawTextureRect(lower.RenderTarget.Texture, args.WorldBounds, DepthTint);
        handle.UseShader(null);

        // Стекло поверх вида вниз: сам пол видно, нижний этаж — сквозь него.
        DrawGlass(handle, map, box, null, 0f);
    }

    /// <summary>
    /// Рисует текстуры стеклянных тайлов карты (полупрозрачные) в мировых координатах. С <paramref name="center"/>
    /// — только в радиусе от него.
    /// </summary>
    internal void DrawGlass(DrawingHandleWorld handle, MapId map, Box2 box, Vector2? center, float radius)
    {
        var maps = _ents.System<SharedMapSystem>();
        var xforms = _ents.System<SharedTransformSystem>();
        _grids.Clear();
        _mapManager.FindGridsIntersecting(map, box, ref _grids);
        foreach (var grid in _grids)
        {
            var tileSize = grid.Comp.TileSize;
            handle.SetTransform(xforms.GetWorldMatrix(grid.Owner));
            foreach (var tile in maps.GetTilesIntersecting(grid.Owner, grid.Comp, box))
            {
                if (!ZLevelTiles.IsTransparent(_tileDefs, tile.Tile) || ZLevelTiles.IsOpenSpace(_tileDefs, tile.Tile))
                    continue;
                if (center is { } c && (maps.GridTileToWorldPos(grid.Owner, grid.Comp, tile.GridIndices) - c).Length() > radius)
                    continue;
                if (_tileDefs[tile.Tile.TypeId] is not ContentTileDefinition { Sprite: { } sprite })
                    continue;
                var texture = _resources.GetResource<TextureResource>(sprite).Texture;
                var px = texture.Height;
                var region = new UIBox2(tile.Tile.Variant * px, 0, (tile.Tile.Variant + 1) * px, px);
                var origin = (Vector2) tile.GridIndices * tileSize;
                handle.DrawTextureRectRegion(texture, new Box2(origin, origin + new Vector2(tileSize, tileSize)), ZLevelTiles.IsSpaceLike(_tileDefs, tile.Tile) ? Color.White : GlassTint, region);
            }
        }
        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    /// Ставит глаза нижних вьюпортов по глазу кадра, который их рисует, до рендера кадра. Иначе нижний этаж
    /// рендерится с прошлым положением камеры и при движении отстаёт от маски — по краю дыр виден космос.
    /// </summary>
    public void PrepareEyes(IEye source)
    {
        if (_lower == null || !TryGetBelow(source.Position.MapId, out var below))
            return;
        var lowerEye = DepthEye(source, below);
        _lower.Eye = lowerEye;
        if (_nestedLower != null && TryGetBelow(below, out var below2))
            _nestedLower.Eye = DepthEye(lowerEye, below2);
    }

    /// <summary>Глаз этажом ниже: та же точка (этажи совмещены), но чуть дальше. Свет — как у основного глаза.</summary>
    internal static Robust.Shared.Graphics.Eye DepthEye(IEye eye, MapId map)
    {
        return new Robust.Shared.Graphics.Eye
        {
            Position = new MapCoordinates(eye.Position.Position, map),
            Zoom = eye.Zoom / DepthScale,
            Rotation = eye.Rotation,
            Offset = eye.Offset,
            DrawFov = false,
            DrawLight = eye.DrawLight,
        };
    }

    /// <summary>
    /// Рисует тайлы сеток карты в их собственных координатах (с поворотом сетки).
    /// <paramref name="opaqueOnly"/> — только непрозрачный пол (открытое пространство и стекло пропускаются).
    /// </summary>
    private bool DrawFloor(DrawingHandleWorld handle, MapId map, Box2 box, Matrix3x2 worldToTarget, Color color, bool opaqueOnly)
    {
        var maps = _ents.System<SharedMapSystem>();
        var xforms = _ents.System<SharedTransformSystem>();
        var drew = false;
        _grids.Clear();
        _mapManager.FindGridsIntersecting(map, box, ref _grids);
        foreach (var grid in _grids)
        {
            var tileSize = grid.Comp.TileSize;
            handle.SetTransform(xforms.GetWorldMatrix(grid.Owner) * worldToTarget);
            foreach (var tile in maps.GetTilesIntersecting(grid.Owner, grid.Comp, box))
            {
                if (opaqueOnly && ZLevelTiles.IsTransparent(_tileDefs, tile.Tile))
                    continue;
                var origin = (Vector2) tile.GridIndices * tileSize;
                handle.DrawRect(new Box2(origin, origin + new Vector2(tileSize, tileSize)), color);
                drew = true;
            }
        }
        return drew;
    }

    private void DrawLadderHoles(DrawingHandleWorld handle, MapId map, Box2 box, Matrix3x2 worldToTarget)
    {
        var xforms = _ents.System<SharedTransformSystem>();
        var query = _ents.EntityQueryEnumerator<ZLadderComponent, TransformComponent>();
        while (query.MoveNext(out _, out var ladder, out var xform))
        {
            if (ladder.Down == null || ladder.Kind != "ladder" || xform.MapID != map)
                continue;
            var (pos, rot) = xforms.GetWorldPositionRotation(xform);
            if (!box.Contains(pos))
                continue;
            handle.SetTransform(Matrix3Helpers.CreateTransform(pos, rot) * worldToTarget);
            handle.DrawRect(Box2.CenteredAround(Vector2.Zero, new Vector2(0.75f, 0.75f)), Color.White);
        }
    }

    /// <summary>Этаж ниже карты, которую сейчас рисуют.</summary>
    private bool TryGetBelow(MapId map, out MapId below)
    {
        below = MapId.Nullspace;
        if (_player.LocalEntity == null)
            return false;
        var maps = _ents.System<SharedMapSystem>();
        if (!maps.TryGetMap(map, out var mapUid)
            || !_ents.TryGetComponent<ZLevelMapComponent>(mapUid, out var z)
            || z.Below is not { } belowUid
            || !_ents.TryGetComponent<MapComponent>(belowUid, out var belowMap))
            return false;
        below = belowMap.MapId;
        return below != MapId.Nullspace;
    }

    /// <summary>Нечего показывать: выключаем рендер этажа ниже (а для основного кадра — и ещё ниже).</summary>
    private void Pause(bool nested)
    {
        if (_nestedLower != null)
            _nestedLower.AutomaticRender = false;
        if (!nested && _lower != null)
            _lower.AutomaticRender = false;
    }

    protected override void DisposeBehavior()
    {
        _lower?.Dispose();
        _stencil?.Dispose();
        _nestedLower?.Dispose();
        _nestedStencil?.Dispose();
        base.DisposeBehavior();
    }
}
