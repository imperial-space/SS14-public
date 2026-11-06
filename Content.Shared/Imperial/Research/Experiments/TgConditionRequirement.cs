using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

public sealed partial class TgConditionRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public TgScanCondition Condition;
}
