using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>ordnance/gaseous: баллон или канистра с заданным газом.</summary>
public sealed partial class TgGasRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public string Gas = string.Empty;

    [DataField]
    public float Moles = 10f;
}
