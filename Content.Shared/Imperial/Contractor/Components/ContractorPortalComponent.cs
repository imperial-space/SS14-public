using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.Contractor.Components;

/// <summary>
/// Temporary contractor falsefire portal that only accepts the assigned target.
/// </summary>
[RegisterComponent]
public sealed partial class ContractorPortalComponent : Component
{
    [DataField]
    public EntityUid ContractorMind;

    [DataField]
    public EntityUid TargetEntity;

    [DataField]
    public float ActivationRange = 0.8f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan ExpireAt;
}