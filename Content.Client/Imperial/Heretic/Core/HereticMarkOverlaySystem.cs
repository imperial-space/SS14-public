using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Imperial.Heretic.Paths.Flesh;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic.Core;

/// <summary>
/// Рисует метки еретика поверх спрайта цели. Какой спрайт рисовать, задаёт сам компонент метки.
/// </summary>
public sealed class HereticMarkOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeMark<AshMarkComponent>(HereticMarkVisualLayers.Ash, c => c.MarkSprite);
        SubscribeMark<HereticBladeMarkComponent>(HereticMarkVisualLayers.Blade, c => c.MarkSprite);
        SubscribeMark<FleshMarkComponent>(HereticMarkVisualLayers.Flesh, c => c.MarkSprite);
        SubscribeMark<LockMarkComponent>(HereticMarkVisualLayers.Lock, c => c.MarkSprite);
        SubscribeMark<RustMarkComponent>(HereticMarkVisualLayers.Rust, c => c.MarkSprite);
        SubscribeMark<VoidMarkComponent>(HereticMarkVisualLayers.Void, c => c.MarkSprite);
        SubscribeMark<MoonMarkComponent>(HereticMarkVisualLayers.Moon, c => c.MarkSprite);
        SubscribeMark<HereticMoonConvertedComponent>(HereticMarkVisualLayers.MoonConverted, c => c.OverlaySprite);

        // Эти метки перерисовываются после смены внешнего вида цели.
        SubscribeMark<CosmosMarkComponent>(HereticMarkVisualLayers.Cosmos, c => c.MarkSprite);
        SubscribeLocalEvent<CosmosMarkComponent, AppearanceChangeEvent>(OnCosmosMarkAppearanceChange);

        SubscribeMark<StarMarkComponent>(HereticMarkVisualLayers.StarRing, c => c.MarkSprite);
        SubscribeLocalEvent<StarMarkComponent, AppearanceChangeEvent>(OnStarMarkAppearanceChange);
    }

    private void OnCosmosMarkAppearanceChange(Entity<CosmosMarkComponent> ent, ref AppearanceChangeEvent args)
    {
        AddOverlay(ent, HereticMarkVisualLayers.Cosmos, ent.Comp.MarkSprite);
    }

    private void OnStarMarkAppearanceChange(Entity<StarMarkComponent> ent, ref AppearanceChangeEvent args)
    {
        AddOverlay(ent, HereticMarkVisualLayers.StarRing, ent.Comp.MarkSprite);
    }

    private void SubscribeMark<T>(HereticMarkVisualLayers key, Func<T, SpriteSpecifier> getSprite)
        where T : IComponent
    {
        SubscribeLocalEvent<T, ComponentStartup>((uid, comp, _) => AddOverlay(uid, key, getSprite(comp)));
        SubscribeLocalEvent<T, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, key));
    }

    private void AddOverlay(EntityUid uid, HereticMarkVisualLayers key, SpriteSpecifier spec)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var ent = (uid, sprite);
        if (!_sprite.LayerMapTryGet(ent, key, out var layer, false))
        {
            layer = _sprite.AddLayer(ent, spec);
            _sprite.LayerMapSet(ent, key, layer);
        }

        sprite.LayerSetShader(layer, "unshaded");
    }

    private void RemoveOverlay(EntityUid uid, HereticMarkVisualLayers key)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.RemoveLayer((uid, sprite), key);
    }
}
