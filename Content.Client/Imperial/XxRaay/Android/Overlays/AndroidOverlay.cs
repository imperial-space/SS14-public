using System.Numerics;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.XxRaay.Android;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.XxRaay.Android;

/// <summary>
/// Оверлей, рисующий иконку над всеми сущностями с <see cref="AndroidStressComponent"/>.
/// </summary>
public sealed partial class AndroidOverlay : Overlay
{
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IResourceCache _resources = default!;

    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _xforms;
    private readonly MapSystem _maps;

    private readonly string _iconSprite;
    private readonly float _iconScale;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public AndroidOverlay(string iconSprite, float iconScale)
    {
        IoCManager.InjectDependencies(this);

        _sprites = _systems.GetEntitySystem<SpriteSystem>();
        _xforms = _systems.GetEntitySystem<SharedTransformSystem>();
        _maps = _systems.GetEntitySystem<MapSystem>();
        _iconSprite = iconSprite;
        _iconScale = iconScale;
        _font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 9);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return;

        switch (args.Space)
        {
            case OverlaySpace.WorldSpace:
                DrawWorld(args);
                break;
            case OverlaySpace.ScreenSpace:
                DrawScreen(args);
                break;
        }
    }

    private void DrawWorld(in OverlayDrawArgs args)
    {
        if (!_maps.TryGetMap(args.MapId, out var mapUid))
            return;

        var worldHandle = args.WorldHandle;
        var texture = _sprites.Frame0(ParseSpriteSpecifier(_iconSprite));

        var worldMatrix = _xforms.GetWorldMatrix(mapUid.Value);
        var invMatrix = _xforms.GetInvWorldMatrix(mapUid.Value);

        var query = _entManager.EntityQueryEnumerator<AndroidStressComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_player.LocalEntity == uid)
                continue;

            var coords = _xforms.GetMapCoordinates(uid);
            if (coords.MapId != args.MapId)
                continue;

            if (!_entManager.TryGetComponent(uid, out AndroidDiodeComponent? diode) || !diode.HasDiode)
                continue;

            var localPos = Vector2.Transform(coords.Position, invMatrix);
            var halfSize = 0.5f * _iconScale;

            var aabb = new Box2(
                localPos - new Vector2(halfSize, halfSize),
                localPos + new Vector2(halfSize, halfSize));

            var box = new Box2Rotated(aabb, Angle.Zero, localPos);

            worldHandle.SetTransform(worldMatrix);
            worldHandle.DrawTextureRect(texture, box, Color.White);
            worldHandle.SetTransform(Matrix3x2.Identity);
        }
    }

    private void DrawScreen(in OverlayDrawArgs args)
    {
        var screenHandle = args.ScreenHandle;

        var query = _entManager.EntityQueryEnumerator<AndroidStressComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_player.LocalEntity == uid)
                continue;

            var coords = _xforms.GetMapCoordinates(uid);
            if (coords.MapId != args.MapId)
                continue;

            if (!_entManager.TryGetComponent(uid, out AndroidDiodeComponent? diode) || !diode.HasDiode)
                continue;

            var worldPos = _xforms.GetWorldPosition(uid);
            var screenPos = _eye.WorldToScreen(worldPos);
            var name = Identity.Name(uid, _entManager);
            var textOffset = new Vector2(16f, -6f);
            screenHandle.DrawString(_font, screenPos + textOffset, name, Color.Cyan);
        }
    }

    private static SpriteSpecifier ParseSpriteSpecifier(string value)
    {
        var split = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length >= 2)
        {
            var state = split[^1];
            var path = string.Join('/', split[..^1]);
            return new SpriteSpecifier.Rsi(new ResPath(path), state);
        }

        return new SpriteSpecifier.Texture(new ResPath(value));
    }
}

