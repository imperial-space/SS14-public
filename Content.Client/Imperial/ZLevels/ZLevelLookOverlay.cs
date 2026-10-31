using System.Numerics;
using Content.Shared.Imperial.ZLevels;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.ZLevels;

/// <summary>
/// Взгляд вверх/вниз: этаж, на который смотрит игрок, рендерится своим вьюпортом с глазом игрока (камера не
/// прыгает, движение предсказано) и рисуется поверх основного кадра, каждый со своим полем зрения.
/// Вниз — целиком (этажи под ним видно через <see cref="ZLevelOverlay"/> в этом вьюпорте).
/// Вверх — только там, где у верхнего этажа есть пол или стекло; сквозь пустоту и открытое пространство остаётся
/// свой этаж из основного кадра, с его стенами и полем зрения.
/// </summary>
public sealed class ZLevelLookOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> StencilMaskShader = "StencilMask";
    private static readonly ProtoId<ShaderPrototype> StencilEqualDrawShader = "StencilEqualDraw";

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    /// <summary>Сколько клеток верхнего этажа видно вокруг себя, подняв голову.</summary>
    private const float LookRadius = 4.5f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>Вьюпорт этажа, на который смотрят.</summary>
    public IClydeViewport? View { get; private set; }
    /// <summary>Какого размера нужен вьюпорт: создаётся в FrameUpdate, не посреди отрисовки.</summary>
    private Vector2i? _wantView;
    private IRenderTexture? _stencil;
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<(Entity<SpriteComponent> Sprite, Vector2 Pos, Angle Rot)> _onGlass = new();

    public ZLevelLookOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = -100;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_wantView is not { } size)
            return;
        View?.Dispose();
        View = _clyde.CreateViewport(size, "zlevel-look");
        View.ClearColor = Color.Black;
        _wantView = null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye != _eyeManager.CurrentEye)
            return;
        if (args.Viewport.Eye is not { } eye || !_ents.System<ZLevelSystem>().TryGetLookedMap(out var map, out var up) || map == args.MapId)
        {
            if (View != null)
                View.AutomaticRender = false;
            return;
        }

        var size = args.Viewport.Size;
        if (View == null || View.Size != size)
        {
            // Clyde перебирает список вьюпортов, пока рисует: новый посреди обхода его ломает. Ждём следующего кадра.
            _wantView = size;
            return;
        }
        View.RenderScale = args.Viewport.RenderScale;
        View.Eye = LookEye(eye, map);
        View.AutomaticRender = true;

        var handle = args.WorldHandle;
        if (!up)
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.DrawTextureRect(View.RenderTarget.Texture, args.WorldBounds);
            return;
        }

        // Маска: клетки верхнего этажа, которые закрывают обзор (всё, кроме пустоты и открытого пространства).
        var target = args.Viewport.RenderTarget;
        if (_stencil?.Texture.Size != target.Size)
        {
            _stencil?.Dispose();
            _stencil = _clyde.CreateRenderTarget(target.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "zlevel-look-stencil");
        }
        var st = _stencil;
        var lightScale = target.Size / (Vector2) args.Viewport.Size;
        var scale = args.Viewport.RenderScale / (Vector2.One / lightScale);
        var box = args.WorldAABB.Enlarged(2f);
        var maps = _ents.System<SharedMapSystem>();
        var xforms = _ents.System<SharedTransformSystem>();
        var any = false;
        // Снизу видно только кусок верхнего этажа вокруг себя, как в жизни.
        var from = _player.LocalEntity is { } player ? xforms.GetWorldPosition(player) : eye.Position.Position;

        handle.RenderInRenderTarget(st, () =>
        {
            var worldToTarget = st.GetWorldToLocalMatrix(eye, scale);
            handle.UseShader(_proto.Index(UnshadedShader).Instance());
            _grids.Clear();
            _mapManager.FindGridsIntersecting(map, box, ref _grids);
            foreach (var grid in _grids)
            {
                var tileSize = grid.Comp.TileSize;
                handle.SetTransform(xforms.GetWorldMatrix(grid.Owner) * worldToTarget);
                foreach (var tile in maps.GetTilesIntersecting(grid.Owner, grid.Comp, box))
                {
                    // Сквозь открытое пространство и стекло виден свой этаж (стекло дорисуется поверх).
                    if (ZLevelTiles.IsTransparent(_tileDefs, tile.Tile))
                        continue;
                    if ((maps.GridTileToWorldPos(grid.Owner, grid.Comp, tile.GridIndices) - from).Length() > LookRadius)
                        continue;
                    var origin = (Vector2) tile.GridIndices * tileSize;
                    handle.DrawRect(new Box2(origin, origin + new Vector2(tileSize, tileSize)), Color.White);
                    any = true;
                }
            }
        }, Color.Transparent);

        if (any)
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(_proto.Index(StencilMaskShader).Instance());
            handle.DrawTextureRect(st.Texture, args.WorldBounds);
            handle.UseShader(_proto.Index(StencilEqualDrawShader).Instance());
            handle.DrawTextureRect(View.RenderTarget.Texture, args.WorldBounds);
            handle.UseShader(null);
        }
        _overlays.GetOverlay<ZLevelOverlay>().DrawGlass(handle, map, box, from, LookRadius);
        DrawOnGlass(handle, new MapCoordinates(from, map), eye.Rotation);
    }

    /// <summary>Те, кто стоит на верхнем стекле: их видно снизу сквозь него (сам этаж под стеклом — основной кадр).</summary>
    private void DrawOnGlass(DrawingHandleWorld handle, MapCoordinates from, Angle eyeRotation)
    {
        var z = _ents.System<ZLevelSystem>();
        var sprites = _ents.System<SpriteSystem>();
        var xforms = _ents.System<SharedTransformSystem>();
        _onGlass.Clear();
        foreach (var uid in _ents.System<EntityLookupSystem>().GetEntitiesInRange(from, LookRadius, LookupFlags.Uncontained))
        {
            if (!_ents.TryGetComponent<SpriteComponent>(uid, out var sprite) || !sprite.Visible)
                continue;
            var (pos, rot) = xforms.GetWorldPositionRotation(uid);
            var tile = z.GetTile(new MapCoordinates(pos, from.MapId), out _, out _);
            if (!ZLevelTiles.IsTransparent(_tileDefs, tile) || ZLevelTiles.IsOpenSpace(_tileDefs, tile))
                continue;
            _onGlass.Add(((uid, sprite), pos, rot));
        }
        // Как в обычной отрисовке: по слою, затем сверху вниз.
        _onGlass.Sort((a, b) =>
        {
            var depth = a.Sprite.Comp.DrawDepth.CompareTo(b.Sprite.Comp.DrawDepth);
            return depth != 0 ? depth : b.Pos.Y.CompareTo(a.Pos.Y);
        });
        handle.SetTransform(Matrix3x2.Identity);
        foreach (var (sprite, pos, rot) in _onGlass)
        {
            // Слой WorldSpace идёт после освещения — затемняем спрайт сами по свету ламп того этажа.
            var original = sprite.Comp.Color;
            sprites.SetColor(sprite.AsNullable(), original * LightAt(new MapCoordinates(pos, from.MapId)));
            sprites.RenderSprite(sprite, handle, eyeRotation, rot, pos);
            sprites.SetColor(sprite.AsNullable(), original);
        }
    }

    /// <summary>
    /// Примерная освещённость точки: фоновый свет карты плюс лампы вокруг (без теней) — чтобы стоящие на стекле
    /// в тёмной комнате наверху не светились, а под лампой были видны как обычно.
    /// </summary>
    private Color LightAt(MapCoordinates coords)
    {
        var maps = _ents.System<SharedMapSystem>();
        var r = 0f;
        var g = 0f;
        var b = 0f;
        if (maps.TryGetMap(coords.MapId, out var map) && _ents.TryGetComponent<MapLightComponent>(map, out var ambient))
        {
            r = ambient.AmbientLightColor.R;
            g = ambient.AmbientLightColor.G;
            b = ambient.AmbientLightColor.B;
        }

        var xforms = _ents.System<SharedTransformSystem>();
        var query = _ents.EntityQueryEnumerator<PointLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var light, out var xform))
        {
            if (!light.Enabled || xform.MapID != coords.MapId || _ents.HasComponent<ZLightProxyComponent>(uid))
                continue;
            var dist = (xforms.GetWorldPosition(xform) - coords.Position).Length();
            if (dist >= light.Radius)
                continue;
            var falloff = 1f - dist / light.Radius;
            var power = light.Energy * falloff * falloff;
            r += light.Color.R * power;
            g += light.Color.G * power;
            b += light.Color.B * power;
        }

        // Совсем в черноту не уводим — силуэт должен угадываться, как в обычной темноте.
        return new Color(Math.Clamp(r, 0.12f, 1f), Math.Clamp(g, 0.12f, 1f), Math.Clamp(b, 0.12f, 1f));
    }

    private static Robust.Shared.Graphics.Eye LookEye(IEye eye, MapId map)
    {
        return new Robust.Shared.Graphics.Eye
        {
            Position = new MapCoordinates(eye.Position.Position, map),
            Zoom = eye.Zoom,
            Rotation = eye.Rotation,
            Offset = eye.Offset,
            DrawFov = eye.DrawFov,
            DrawLight = eye.DrawLight,
        };
    }

    /// <summary>Глаз вьюпорта взгляда до рендера кадра (см. <see cref="ZLevelOverlay.PrepareEyes"/>).</summary>
    public IEye? PrepareEye(IEye eye, MapId map)
    {
        if (View == null)
            return null;
        View.Eye = LookEye(eye, map);
        return View.Eye;
    }

    protected override void DisposeBehavior()
    {
        View?.Dispose();
        _stencil?.Dispose();
        base.DisposeBehavior();
    }
}
