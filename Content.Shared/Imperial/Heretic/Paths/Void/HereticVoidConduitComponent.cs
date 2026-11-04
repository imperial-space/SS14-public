namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent]
public sealed partial class HereticVoidConduitComponent : Component
{
    [DataField]
    public EntityUid? Caster;

    [DataField]
    public float PulseInterval = 2f;

    [DataField]
    public float WaveRadius = 8f;

    [DataField]
    public float Lifetime = 60f;

    [DataField]
    public int StructureDamageMin = 15;

    [DataField]
    public int StructureDamageMax = 30;

    [ViewVariables]
    public float PulseTimer;
    [ViewVariables]
    public float LifetimeTimer;
    [ViewVariables]
    public EntityUid? AmbientSound;
}
