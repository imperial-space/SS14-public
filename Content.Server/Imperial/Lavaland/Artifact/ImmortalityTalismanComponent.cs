using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.Artifact;

[RegisterComponent]
[Access(typeof(ImmortalityTalismanSystem))]
public sealed partial class ImmortalityTalismanComponent : Component
{
    [DataField]
    public float GodmodeDurationSeconds = 8f;

    [DataField]
    public float CooldownSeconds = 80f;

    [DataField]
    public bool IsActive;

    [DataField]
    public EntityUid? Holder;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan GodmodeEndTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan CooldownEndTime;
}
