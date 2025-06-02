using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.LeaveNoTrace;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class LeaveNoTraceComponent : Component
{
    [DataField]
    public float TimeForReveal = 5f;

    [DataField]
    public EntProtoId LeaveNoTraceObjective = "LeaveNoTraceObjective";

    [DataField]
    public float Range = 12f;

    [ViewVariables]
    public bool IsSeen = false;

    [ViewVariables]
    public float? CurTime;

    [DataField]
    public string Effect = "WhistleNinjaExclamation";

    [ViewVariables]
    public NetEntity? EffectEntity;
}
