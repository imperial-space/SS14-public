namespace Content.Shared.Imperial.Heretic.Paths.Void;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HereticVoidConduitComponent : Component
{
    [DataField]
    public EntityUid? Caster;

    [DataField]
    public TimeSpan PulseInterval = TimeSpan.FromSeconds(2f);

    [DataField]
    public float WaveRadius = 8f;

    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(60f);

    [DataField]
    public int StructureDamageMin = 15;

    [DataField]
    public int StructureDamageMax = 30;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextPulse;
    [ViewVariables, AutoPausedField]
    public TimeSpan LifetimeEnd;
    [ViewVariables]
    public EntityUid? AmbientSound;
}
