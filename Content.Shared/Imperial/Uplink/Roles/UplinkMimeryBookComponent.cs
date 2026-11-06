using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Книга мима: даёт заклинание, если читатель — мим.</summary>
[RegisterComponent]
public sealed partial class UplinkMimeryBookComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;
}
