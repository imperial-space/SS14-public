using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticStarMarkOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi StarMarkSpec = new(
        new ResPath("Imperial/heretic/eldritch_fx.rsi"),
        "cosmic_ring");

    private enum StarMarkKey { Ring }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StarMarkComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StarMarkComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StarMarkComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(EntityUid uid, StarMarkComponent _, ComponentStartup args)
        => AddOverlay(uid);

    private void OnAppearanceChange(EntityUid uid, StarMarkComponent _, ref AppearanceChangeEvent args)
        => AddOverlay(uid);

    private void AddOverlay(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var ent = (uid, sprite);
        if (!_sprite.LayerMapTryGet(ent, StarMarkKey.Ring, out var layer, false))
        {
            layer = _sprite.AddLayer(ent, StarMarkSpec);
            _sprite.LayerMapSet(ent, StarMarkKey.Ring, layer);
        }

        sprite.LayerSetShader(layer, "unshaded");
    }

    private void OnShutdown(EntityUid uid, StarMarkComponent _, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.RemoveLayer((uid, sprite), StarMarkKey.Ring);
    }
}
