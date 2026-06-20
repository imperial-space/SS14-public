using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.Chaos;

[RegisterComponent]
public sealed partial class ChaosBottleComponent : Component
{
    [DataField]
    public float Radius = 8f;

    [DataField]
    public EntProtoId ChainsawPrototype = "Chainsaw";

    [DataField]
    public float StimulantsAmount = 10f;

    [DataField]
    public float RageDurationSeconds = 120f;

    [DataField]
    public string RipAndTearObjective = "ChaosRipAndTearTargetObjective";
}
