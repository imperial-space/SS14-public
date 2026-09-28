using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Heretic.Components;

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

    public float PulseTimer;
    public float LifetimeTimer;
    public EntityUid? AmbientSound;
}

[RegisterComponent]
public sealed partial class VoidConduitPressureComponent : Component
{
    public TimeSpan ExpiresAt;
}
