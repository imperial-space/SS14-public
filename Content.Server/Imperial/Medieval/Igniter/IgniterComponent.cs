using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Igniter;


[RegisterComponent]
public sealed partial class IgniterComponent : Component
{
    /// <summary>
    /// Time for ignite with <see cref="IgniteChance" />
    /// </summary>
    [DataField]
    public TimeSpan IgniteTime = TimeSpan.Zero;

    /// <summary>
    /// Chance to ignite every <see cref="IgniteTime" />
    /// <para>
    /// Dosent work if <see cref="IgniteTime" /> equals zero
    /// </para>
    /// </summary>
    [DataField]
    public float IgniteChance = 1.0f;

    /// <summary>
    /// The number of stacks of fire that will be applied to an entity when set on fire.
    /// </summary>
    [DataField]
    public float FlammableStacks = 0.5f;

    /// <summary>
    /// The sound that will be played every time a fire is attempted.
    /// </summary>
    [DataField]
    public SoundSpecifier? IgniteSound;

    /// <summary>
    /// Entity effect spawned every ignite attempt
    /// </summary>
    [DataField]
    public EntProtoId? EffectPrototype;
}
