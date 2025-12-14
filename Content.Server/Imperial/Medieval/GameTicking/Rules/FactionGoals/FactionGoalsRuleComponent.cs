using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent]
public sealed partial class FactionGoalsRuleComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<MedievalFactionPrototype>, List<ProtoId<FactionGoalsPackPrototype>>> Goals = new();
}

