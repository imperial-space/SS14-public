using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Attached to the status effect entity. Causes periodic brief stuns (insanity jitter).
/// Applied by Ash path Mansus Grasp mark.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class HereticInsanityStatusEffectComponent : Component
{
    [DataField]
    public float JitterIntervalMin = 3f;

    [DataField]
    public float JitterIntervalMax = 6f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextJitterTime = TimeSpan.Zero;
}
