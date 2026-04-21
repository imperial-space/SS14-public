using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.ODM.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class OdmUserComponent : Component
{
    [DataField]
    public EntityUid Gear;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? LastImpact;
}