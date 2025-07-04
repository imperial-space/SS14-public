using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.LeaveNoTrace;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class LeaveNoTraceComponent : Component
{
    /// <summary>
    /// The distance from which a ninja can be spotted
    /// </summary>
    [DataField]
    public float Range = 12f;

    /// <summary>
    /// Text that appears after revealing a ninja
    /// </summary>
    [DataField]
    public LocId RevealText = "imperial-ninja-reveal";

    /// <summary>
    /// Texture params
    /// </summary>
    [DataField]
    public TextureGlitchParametersData TextureParams = new();

    /// <summary>
    /// Text glitch shader params
    /// </summary>
    [DataField]
    public GlitchShaderParametersData TextGlitchEffectParams = new();

    /// <summary>
    /// Frequency of calling the Update method. Necessary for optimization
    /// </summary>
    [DataField]
    public TimeSpan VisibilityCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Time it takes for a ninja to be revealed
    /// </summary>
    [DataField]
    public TimeSpan TimeForReveal = TimeSpan.FromSeconds(5);



    /// <summary>
    /// Entities that can currently see ninja
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> WitnessEntities = new();

    /// <summary>
    /// Is the ninja visible?
    /// </summary>
    [ViewVariables]
    public bool IsSeen = false;

    [DataField(readOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextVisibilityCheck = TimeSpan.Zero;

    [DataField(readOnly: true, customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? RevealEndTime;
}
