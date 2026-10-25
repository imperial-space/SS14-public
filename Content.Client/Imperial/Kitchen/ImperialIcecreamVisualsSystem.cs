using Content.Shared.Imperial.Kitchen;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Kitchen;

/// <summary>Шарик мороженого поверх рожка, окрашенный цветом вкуса (icecream_custom TG).</summary>
public sealed class ImperialIcecreamVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string Layer = "scoop";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialIcecreamConeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ImperialIcecreamConeComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnStartup(Entity<ImperialIcecreamConeComponent> ent, ref ComponentStartup args) => Update(ent);

    private void OnState(Entity<ImperialIcecreamConeComponent> ent, ref AfterAutoHandleStateEvent args) => Update(ent);

    private void Update(Entity<ImperialIcecreamConeComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !_sprite.LayerMapTryGet((ent, sprite), Layer, out var layer, false))
            return;

        _sprite.LayerSetVisible((ent, sprite), layer, ent.Comp.Scoops > 0);
        if (ent.Comp.Scoop is { } color)
            _sprite.LayerSetColor((ent, sprite), layer, color);
    }
}
