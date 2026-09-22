using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticPathMarkOverlaySystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi AshSpec   = new(new ResPath("Imperial/heretic/tag.rsi"), "ash");
    private static readonly SpriteSpecifier.Rsi BladeSpec = new(new ResPath("Imperial/heretic/tag.rsi"), "blade");
    private static readonly SpriteSpecifier.Rsi FleshSpec = new(new ResPath("Imperial/heretic/tag.rsi"), "flesh");
    private static readonly SpriteSpecifier.Rsi LockSpec  = new(new ResPath("Imperial/heretic/tag.rsi"), "lock");
    private static readonly SpriteSpecifier.Rsi RustSpec  = new(new ResPath("Imperial/heretic/tag.rsi"), "rust");
    private static readonly SpriteSpecifier.Rsi VoidSpec  = new(new ResPath("Imperial/heretic/tag.rsi"), "void");
    private static readonly SpriteSpecifier.Rsi MoonSpec  = new(new ResPath("Imperial/heretic/eldritch_fx.rsi"), "moon_insanity_overlay");

    private enum AshMarkKey   { Mark }
    private enum BladeMarkKey { Mark }
    private enum FleshMarkKey { Mark }
    private enum LockMarkKey  { Mark }
    private enum RustMarkKey  { Mark }
    private enum VoidMarkKey  { Mark }
    private enum MoonMarkKey  { Mark }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, AshMarkKey.Mark, AshSpec));
        SubscribeLocalEvent<AshMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, AshMarkKey.Mark));

        SubscribeLocalEvent<HereticBladeMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, BladeMarkKey.Mark, BladeSpec));
        SubscribeLocalEvent<HereticBladeMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, BladeMarkKey.Mark));

        SubscribeLocalEvent<FleshMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, FleshMarkKey.Mark, FleshSpec));
        SubscribeLocalEvent<FleshMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, FleshMarkKey.Mark));

        SubscribeLocalEvent<LockMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, LockMarkKey.Mark, LockSpec));
        SubscribeLocalEvent<LockMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, LockMarkKey.Mark));

        SubscribeLocalEvent<RustMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, RustMarkKey.Mark, RustSpec));
        SubscribeLocalEvent<RustMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, RustMarkKey.Mark));

        SubscribeLocalEvent<VoidMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, VoidMarkKey.Mark, VoidSpec));
        SubscribeLocalEvent<VoidMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, VoidMarkKey.Mark));

        SubscribeLocalEvent<MoonMarkComponent, ComponentStartup>((uid, _, _) => AddOverlay(uid, MoonMarkKey.Mark, MoonSpec));
        SubscribeLocalEvent<MoonMarkComponent, ComponentShutdown>((uid, _, _) => RemoveOverlay(uid, MoonMarkKey.Mark));
    }

    private void AddOverlay<TKey>(EntityUid uid, TKey key, SpriteSpecifier.Rsi spec) where TKey : Enum
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

    private void RemoveOverlay<TKey>(EntityUid uid, TKey key) where TKey : Enum
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.RemoveLayer((uid, sprite), key);
    }
}
