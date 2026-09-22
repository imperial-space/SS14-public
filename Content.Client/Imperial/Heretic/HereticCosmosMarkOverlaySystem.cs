using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCosmosMarkOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi CosmosMarkSpec = new(
        new ResPath("Imperial/heretic/tag.rsi"),
        "cosmos");

    private enum CosmosMarkKey { Mark }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CosmosMarkComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CosmosMarkComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CosmosMarkComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(EntityUid uid, CosmosMarkComponent _, ComponentStartup args)
        => AddOverlay(uid);

    private void OnAppearanceChange(EntityUid uid, CosmosMarkComponent _, ref AppearanceChangeEvent args)
        => AddOverlay(uid);

    private void AddOverlay(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var ent = (uid, sprite);
        if (!_sprite.LayerMapTryGet(ent, CosmosMarkKey.Mark, out var layer, false))
        {
            layer = _sprite.AddLayer(ent, CosmosMarkSpec);
            _sprite.LayerMapSet(ent, CosmosMarkKey.Mark, layer);
        }

        sprite.LayerSetShader(layer, "unshaded");
    }

    private void OnShutdown(EntityUid uid, CosmosMarkComponent _, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.RemoveLayer((uid, sprite), CosmosMarkKey.Mark);
    }
}
