using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Тело битраннера, пока его разум в аватаре.</summary>
[RegisterComponent]
public sealed partial class BitrunningPilotComponent : Component
{
    [ViewVariables]
    public EntityUid Avatar;
}
