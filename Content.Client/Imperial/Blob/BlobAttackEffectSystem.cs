using System.Numerics;
using Content.Shared.Imperial.Blob.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Imperial.Blob;

/// <summary>do_attack_animation(): 0.2 с к цели и 0.2 с обратно.</summary>
public sealed class BlobAttackEffectSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    private const string AnimationKey = "blob-attack-lunge";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobAttackEffectComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<BlobAttackEffectComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<BlobAttackEffectComponent> ent, ref ComponentStartup args) => Play(ent);

    private void OnState(Entity<BlobAttackEffectComponent> ent, ref AfterAutoHandleStateEvent args) => Play(ent);

    private void Play(Entity<BlobAttackEffectComponent> ent)
    {
        if (ent.Comp.Lunge == Vector2.Zero || !HasComp<SpriteComponent>(ent) || _animation.HasRunningAnimation(ent, AnimationKey))
            return;

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(0.4),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(ent.Comp.Lunge, 0.2f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.2f),
                    },
                },
            },
        };

        _animation.Play(ent.Owner, animation, AnimationKey);
    }
}
