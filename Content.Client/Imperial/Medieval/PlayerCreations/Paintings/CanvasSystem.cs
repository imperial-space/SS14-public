
using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;


namespace Content.Client.Imperial.Medieval.PlayerCreations.Paintings;
public sealed class CanvasSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CanvasTextureChangedEvent>(OnTextureChanged);

        SubscribeLocalEvent<CanvasComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, CanvasComponent comp, ComponentStartup args)
    {
        SetTexture(uid, comp.Texture);
    }

    private void SetTexture(EntityUid uid, Color[] colors)
    {
        var texture = PaintingHelper.GetTextureFromColorArray(_clyde, colors);

        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.LayerSetTexture((uid, sprite), 1, texture);
    }

    private void OnTextureChanged(CanvasTextureChangedEvent args)
    {
        var uid = GetEntity(args.Canvas);

        if (!TryComp<CanvasComponent>(uid, out var canvas))
            return;

        canvas.Texture = args.Texture;
        SetTexture(uid, args.Texture);
    }
}
