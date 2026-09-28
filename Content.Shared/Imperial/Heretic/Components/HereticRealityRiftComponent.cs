namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Marks a Reality Rift entity that a heretic can absorb for knowledge points.
/// </summary>
[RegisterComponent]
public sealed partial class HereticRealityRiftComponent : Component
{
    /// <summary>Knowledge points granted when absorbed.</summary>
    [DataField]
    public int KnowledgeGain = 1;

    /// <summary>Time in seconds the heretic must channel to absorb the rift.</summary>
    [DataField]
    public float AbsorbTime = 8f;

    /// <summary>Seconds before the rift respawns as a visible breach after being absorbed.</summary>
    [DataField]
    public float RespawnDelay = 30f;
}
