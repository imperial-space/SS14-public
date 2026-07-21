namespace Content.Shared.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
public sealed partial class HelmComponent : Component
{
    [DataField("helmRotation")]
    public float HelmRotation;

    [DataField("rotationStep")]
    public float RotationStep = 50f;

    [DataField("steeringAngleForMaxTurn")]
    public float SteeringAngleForMaxTurn = 45f;

    [DataField("turnImpulseScalar")]
    public float TurnImpulseScalar = 20f;

    [DataField("stabilizingImpulseScalar")]
    public float StabilizingImpulseScalar = 80f;

    [DataField("minMotionFactor")]
    public float MinMotionFactor = 0.25f;

    [DataField("minShipWeight")]
    public float MinShipWeight = 10f;

    [DataField("OverloadCeilPerTile")]
    public float OverloadCeilPerTile = 20f;
}
