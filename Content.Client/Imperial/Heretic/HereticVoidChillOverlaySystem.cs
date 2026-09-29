using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticVoidChillOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VoidChillComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VoidChillComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VoidChillComponent, AfterAutoHandleStateEvent>(OnStateChanged);
    }

    private void OnStartup(Entity<VoidChillComponent> ent, ref ComponentStartup args)
    {
        UpdateOverlay(ent);
    }

    private void OnStateChanged(Entity<VoidChillComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateOverlay(ent);
    }

    private void UpdateOverlay(Entity<VoidChillComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        var spec = ent.Comp.Stacks >= VoidChillStatusEffectComponent.MaxStacks
            ? ent.Comp.MaxStacksSprite
            : ent.Comp.PartialSprite;

        if (_sprite.LayerMapTryGet(spriteEnt, HereticMarkVisualLayers.VoidChill, out var layer, false))
        {
            _sprite.LayerSetSprite(spriteEnt, layer, spec);
            return;
        }

        layer = _sprite.AddLayer(spriteEnt, spec);
        _sprite.LayerMapSet(spriteEnt, HereticMarkVisualLayers.VoidChill, layer);
        sprite.LayerSetShader(layer, "unshaded");
    }

    private void OnShutdown(Entity<VoidChillComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.RemoveLayer((ent.Owner, sprite), HereticMarkVisualLayers.VoidChill);
    }
}
