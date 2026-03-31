using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Contractor.Components;

[RegisterComponent]
public sealed partial class ContractorPinpointerComponent : Component
{
    [DataField]
    public EntityUid? ContractorMind;

    [DataField]
    public EntityUid? DecoyEntity;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextRefreshAt;

    [DataField]
    public EntityUid? TargetEntity;
}