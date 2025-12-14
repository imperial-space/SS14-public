using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Factions.Components;

[RegisterComponent]
public sealed partial class GallowsComponent : Component
{
    [DataField]
    public ProtoId<MedievalFactionPrototype>? OwningFaction;
}

