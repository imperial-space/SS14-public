using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>Чем выполняется эксперимент.</summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TgExperimentRequirement
{
    /// <summary>Сколько нужно (сканов, очков, побед).</summary>
    [DataField]
    public int Count = 1;
}
