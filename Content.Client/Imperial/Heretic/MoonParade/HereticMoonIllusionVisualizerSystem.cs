using Content.Shared.Imperial.Heretic.MoonParade;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Heretic.MoonParade;

public sealed class HereticMoonIllusionVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonIllusionComponent, AfterAutoHandleStateEvent>(OnAfterState);
    }

    private void OnAfterState(Entity<HereticMoonIllusionComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.ClothingLayersApplied || ent.Comp.ClothingLayers.Count == 0)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.ClothingLayersApplied = true;
        foreach (var layer in ent.Comp.ClothingLayers)
        {
            _sprite.AddLayer((ent, sprite), layer, null);
        }
    }
}
