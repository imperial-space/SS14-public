namespace Content.Server.Imperial.Blob.Components;

[RegisterComponent]
public sealed partial class BlobSporeComponent : Component
{
    [DataField]
    public float InfectionRange = 0.8f;

    [DataField]
    public float TargetSearchRange = 20f;

    [DataField]
    public float ChaseSpeed = 3.5f;

    [DataField]
    public float LatchDuration = 5f;

    [DataField]
    public float InfectionInterval = 1.0f;

    [ViewVariables]
    public float InfectionAccumulator;

    [ViewVariables]
    public EntityUid? LatchTarget;

    [ViewVariables]
    public bool LatchInProgress;

    [ViewVariables]
    public EntityUid? SourceFactory;
}