using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using System.Numerics;
using Content.Shared.Imperial.Aquila.Backflip;

namespace Content.Client.Imperial.Aquila.Backflip;

public sealed class BackflipVisualsSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    private const string AnimationKey = "backflip";
    private const float Height = 0.4f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlayBackflipAnimationEvent>(OnPlay);
    }

    private void OnPlay(PlayBackflipAnimationEvent ev)
    {
        var uid = GetEntity(ev.Entity);

        if (!TryComp<SpriteComponent>(uid, out _))
            return;

        EnsureComp<AnimationPlayerComponent>(uid);

        _animation.Stop(uid, AnimationKey);
        _animation.Play(uid, BuildAnimation(), AnimationKey);
    }

    private static Animation BuildAnimation()
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(0.5f),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(0), 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(90), 0.125f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(180), 0.125f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(270), 0.125f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(360), 0.125f),
                    },
                },

                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0, Height), 0.25f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.25f),
                    },
                },
            },
        };
    }
}
