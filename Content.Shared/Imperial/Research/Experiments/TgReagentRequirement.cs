using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>scanning/reagent: ёмкость с реагентом.</summary>
public sealed partial class TgReagentRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public string Reagent = string.Empty;

    [DataField]
    public float MinAmount = 1f;
}
