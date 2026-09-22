using Content.Shared.Imperial.Heretic.MoonParade;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.Imperial.Heretic.MoonParade;

public sealed class HereticMoonIllusionVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _initialized = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMoonIllusionComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<HereticMoonIllusionComponent, ComponentRemove>(OnRemove);
    }

    private void OnAfterState(EntityUid uid, HereticMoonIllusionComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (_initialized.Contains(uid) || comp.ClothingLayers.Count == 0)
            return;
        _initialized.Add(uid);

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        foreach (var layer in comp.ClothingLayers)
            _sprite.AddLayer((uid, sprite), layer, null);
    }

    private void OnRemove(EntityUid uid, HereticMoonIllusionComponent _, ComponentRemove args)
    {
        _initialized.Remove(uid);
    }
}
