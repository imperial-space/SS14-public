using System;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Robust.Client.Animations;

namespace Content.Client.Imperial.Medieval.Ships.Sail;

[RegisterComponent]
[Access(typeof(SailVisualizerSystem))]
public sealed partial class SailVisualizerComponent : Component
{
    public const string AnimationKey = "sail-toggle";

    [ViewVariables]
    public bool LastFoldedInitialized;

    [ViewVariables]
    public bool LastFolded;

    [DataField]
    public string UnfastenState = "unfasten";

    [DataField]
    public string FastenState = "fasten";

    [DataField]
    public float AnimationLength = 1.2f;

    [ViewVariables]
    public Animation UnfastenAnimation = default!;

    [ViewVariables]
    public Animation FastenAnimation = default!;

    public void BuildAnimations()
    {
        UnfastenAnimation = CreateAnimation(UnfastenState);
        FastenAnimation = CreateAnimation(FastenState);
    }

    private Animation CreateAnimation(string state)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(AnimationLength),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = SailVisualLayers.Animation,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(state, 0f)
                    }
                }
            }
        };
    }
}
