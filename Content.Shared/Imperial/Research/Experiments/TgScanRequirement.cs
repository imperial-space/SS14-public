using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>scanning, scanning/points: отсканировать разные объекты (каждый — один раз).</summary>
public sealed partial class TgScanRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public List<TgScanTarget> Targets = new();
}
