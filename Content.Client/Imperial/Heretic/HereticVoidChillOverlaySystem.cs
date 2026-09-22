using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticVoidChillOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi PartialSpec = new(
        new ResPath("Imperial/heretic/void.rsi"),
        "void_chill_partial");

    private static readonly SpriteSpecifier.Rsi OhFuckSpec = new(
        new ResPath("Imperial/heretic/void.rsi"),
        "void_chill_oh_fuck");

    private enum VoidChillKey { Overlay }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VoidChillComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VoidChillComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VoidChillComponent, AfterAutoHandleStateEvent>(OnStateChanged);
    }

    private void OnStartup(EntityUid uid, VoidChillComponent comp, ComponentStartup args)
        => UpdateOverlay(uid, comp);

    private void OnStateChanged(EntityUid uid, VoidChillComponent comp, ref AfterAutoHandleStateEvent args)
        => UpdateOverlay(uid, comp);

    private void UpdateOverlay(EntityUid uid, VoidChillComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var ent = (uid, sprite);
        var spec = comp.Stacks >= VoidChillStatusEffectComponent.MaxStacks ? OhFuckSpec : PartialSpec;

        if (_sprite.LayerMapTryGet(ent, VoidChillKey.Overlay, out var layer, false))
        {
            sprite.LayerSetState(layer, spec.RsiState);
        }
        else
        {
            layer = _sprite.AddLayer(ent, spec);
            _sprite.LayerMapSet(ent, VoidChillKey.Overlay, layer);
            sprite.LayerSetShader(layer, "unshaded");
        }
    }

    private void OnShutdown(EntityUid uid, VoidChillComponent comp, ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        _sprite.RemoveLayer((uid, sprite), VoidChillKey.Overlay);
    }
}
