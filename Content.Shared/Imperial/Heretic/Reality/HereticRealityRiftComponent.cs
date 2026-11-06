namespace Content.Shared.Imperial.Heretic.Reality;

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
    public TimeSpan AbsorbTime = TimeSpan.FromSeconds(8f);

    /// <summary>Seconds before the rift respawns as a visible breach after being absorbed.</summary>
    [DataField]
    public TimeSpan RespawnDelay = TimeSpan.FromSeconds(30f);

    /// <summary>Серверное: когда разлом последний раз действовал на каждого еретика рядом.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> LastTriggerTimes = new();
}
