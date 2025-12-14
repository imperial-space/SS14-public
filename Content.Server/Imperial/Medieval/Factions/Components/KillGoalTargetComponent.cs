using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Factions.Components;

[RegisterComponent]
public sealed partial class KillGoalTargetComponent : Component
{
    [DataField(required: true)]
    public string TargetId = string.Empty;

    public ProtoId<MedievalFactionPrototype>? LastHitFaction;
}

