using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticMoonConvertedOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi MoonConvertedSpec = new(
        new ResPath("Imperial/heretic/eldritch_fx.rsi"),
        "moon_insanity_overlay");

    private enum MoonConvertedKey { Overlay }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonConvertedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HereticMoonConvertedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, HereticMoonConvertedComponent _, ComponentStartup args)
        => AddOverlay(uid);

    private void AddOverlay(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)) return;
        var ent = (uid, sprite);
        if (_sprite.LayerMapTryGet(ent, MoonConvertedKey.Overlay, out _, false)) return;
        var layer = _sprite.AddLayer(ent, MoonConvertedSpec);
        _sprite.LayerMapSet(ent, MoonConvertedKey.Overlay, layer);
        sprite.LayerSetShader(layer, "unshaded");
    }

    private void OnShutdown(EntityUid uid, HereticMoonConvertedComponent _, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)) return;
        _sprite.RemoveLayer((uid, sprite), MoonConvertedKey.Overlay);
    }
}
