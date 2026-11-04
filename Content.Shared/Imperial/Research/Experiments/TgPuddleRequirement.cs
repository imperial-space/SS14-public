using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>scanning/random/janitor_trash: лужи с одним из реагентов (рвота, кровь).</summary>
public sealed partial class TgPuddleRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public List<string> Reagents = new();
}
