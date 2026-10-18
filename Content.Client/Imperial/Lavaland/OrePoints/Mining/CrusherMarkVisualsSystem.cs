using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Lavaland.OrePoints.Mining;

/// <summary>obj/effect/abstract/crusher_mark: щит под отмеченным существом.</summary>
public sealed class CrusherMarkVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrusherMarkComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CrusherMarkComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<CrusherMarkComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var layer = _sprite.LayerMapReserve((ent, sprite), CrusherMarkKey.Key);
        _sprite.LayerSetRsi((ent, sprite), layer, ent.Comp.Effect.RsiPath, ent.Comp.Effect.RsiState);
    }

    private void OnShutdown(Entity<CrusherMarkComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !_sprite.LayerMapTryGet((ent, sprite), CrusherMarkKey.Key, out var layer, false))
            return;

        _sprite.RemoveLayer((ent, sprite), layer);
    }

    private enum CrusherMarkKey : byte
    {
        Key,
    }
}
