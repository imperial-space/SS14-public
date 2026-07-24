using System.Numerics;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using ContentDrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Визуально опрокидывает использованный торговый автомат и оставляет его спрайт над жертвой.
/// </summary>
public sealed class DeathNoteFallenVendingVisualSystem : EntitySystem
{
    private const string AnimationKey = "death-note-vending-fall";

    [Dependency] private readonly AnimationPlayerSystem _animations = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeathNoteFallenVendingVisualComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(
        Entity<DeathNoteFallenVendingVisualComponent> ent,
        ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        _sprites.SetDrawDepth((ent.Owner, sprite), (int) ContentDrawDepth.OverMobs);

        var player = EnsureComp<AnimationPlayerComponent>(ent);
        _animations.Play(
            (ent.Owner, player),
            CreateFallAnimation(
                sprite.Scale,
                ent.Comp.FallDuration,
                ent.Comp.LiftScale,
                ent.Comp.ImpactHeightScale,
                ent.Comp.ImpactProgress),
            AnimationKey);
    }

    private static Animation CreateFallAnimation(
        Vector2 originalScale,
        TimeSpan configuredDuration,
        float configuredLiftScale,
        float configuredImpactHeightScale,
        float configuredImpactProgress)
    {
        var duration = configuredDuration > TimeSpan.Zero
            ? configuredDuration
            : TimeSpan.FromMilliseconds(1);
        var liftScale = Math.Max(0f, configuredLiftScale);
        var impactHeightScale = Math.Max(0f, configuredImpactHeightScale);
        var impactProgress = Math.Clamp(configuredImpactProgress, 0f, 1f);
        var durationSeconds = (float) duration.TotalSeconds;
        var impactScale = new Vector2(originalScale.X * liftScale, originalScale.Y * impactHeightScale);
        return new Animation
        {
            Length = duration,
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(originalScale, 0f),
                        new AnimationTrackProperty.KeyFrame(originalScale * liftScale, durationSeconds * impactProgress),
                        new AnimationTrackProperty.KeyFrame(impactScale, durationSeconds),
                    },
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                },
            },
        };
    }
}
