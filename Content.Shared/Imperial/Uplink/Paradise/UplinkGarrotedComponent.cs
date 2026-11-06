using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Жертва гарроты: почти не может сдвинуться.</summary>
[RegisterComponent]
public sealed partial class UplinkGarrotedComponent : Component
{
    [ViewVariables]
    public EntityUid Garrote;
}
