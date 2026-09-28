using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Attached to the status effect entity. Causes periodic cold damage ticks.
/// Also slows movement via MovementModStatusEffectComponent in the same entity prototype.
/// Applied by Void path Mansus Grasp mark.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class VoidChillStatusEffectComponent : Component
{
    public const int MaxStacks = 5;

    [DataField]
    public int Stacks = 0;

    [DataField]
    public float TickInterval = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextTickTime = TimeSpan.Zero;
}
