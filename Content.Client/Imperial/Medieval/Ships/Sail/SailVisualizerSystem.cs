using Content.Shared.Imperial.Medieval.Ships.Sail;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Ships.Sail;

public sealed class SailVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SailComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SailComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<SailComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnComponentInit(EntityUid uid, SailComponent component, ComponentInit args)
    {
        EnsureComp<AnimationPlayerComponent>(uid);
        EnsureComp<SailVisualizerComponent>(uid).BuildAnimations();
    }

    private void OnAppearanceChange(EntityUid uid, SailComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<bool>(uid, SailVisuals.Folded, out var folded, args.Component))
            return;

        var visualizer = EnsureVisualizer(uid);

        if (!visualizer.LastFoldedInitialized)
        {
            visualizer.LastFoldedInitialized = true;
            visualizer.LastFolded = folded;
            SetStaticLayers(uid, args.Sprite, folded);
            return;
        }

        var lastFolded = visualizer.LastFolded;
        visualizer.LastFolded = folded;

        if (lastFolded == folded)
        {
            if (!_animation.HasRunningAnimation(uid, SailVisualizerComponent.AnimationKey))
                SetStaticLayers(uid, args.Sprite, folded);

            return;
        }

        PlayToggleAnimation(uid, args.Sprite, visualizer, folded);
    }

    private void OnAnimationCompleted(EntityUid uid, SailComponent component, AnimationCompletedEvent args)
    {
        if (args.Key != SailVisualizerComponent.AnimationKey || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var folded = component.Folded;
        if (TryComp<AppearanceComponent>(uid, out var appearance) &&
            _appearance.TryGetData<bool>(uid, SailVisuals.Folded, out var visualFolded, appearance))
        {
            folded = visualFolded;
        }

        SetStaticLayers(uid, sprite, folded);
    }

    private void PlayToggleAnimation(EntityUid uid, SpriteComponent sprite, SailVisualizerComponent visualizer, bool targetFolded)
    {
        var player = EnsureComp<AnimationPlayerComponent>(uid);
        if (_animation.HasRunningAnimation(uid, player, SailVisualizerComponent.AnimationKey))
            _animation.Stop(uid, player, SailVisualizerComponent.AnimationKey);

        _sprite.LayerSetVisible((uid, sprite), SailVisualLayers.Unfolded, false);
        _sprite.LayerSetVisible((uid, sprite), SailVisualLayers.Folded, false);
        _sprite.LayerSetVisible((uid, sprite), SailVisualLayers.Animation, true);

        var animation = targetFolded ? visualizer.FastenAnimation : visualizer.UnfastenAnimation;
        _animation.Play((uid, player), animation, SailVisualizerComponent.AnimationKey);
    }

    private void SetStaticLayers(EntityUid uid, SpriteComponent sprite, bool folded)
    {
        _sprite.LayerSetVisible((uid, sprite), SailVisualLayers.Unfolded, !folded);
        _sprite.LayerSetVisible((uid, sprite), SailVisualLayers.Folded, folded);
        _sprite.LayerSetVisible((uid, sprite), SailVisualLayers.Animation, false);
    }

    private SailVisualizerComponent EnsureVisualizer(EntityUid uid)
    {
        var visualizer = EnsureComp<SailVisualizerComponent>(uid);
        if (visualizer.UnfastenAnimation == null || visualizer.FastenAnimation == null)
            visualizer.BuildAnimations();

        return visualizer;
    }
}
