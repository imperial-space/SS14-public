using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Factions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalFactionRelationsRequestComponent : Component
{
    [AutoNetworkedField]
    public ProtoId<MedievalFactionPrototype> From = string.Empty;

    [AutoNetworkedField]
    public ProtoId<MedievalFactionPrototype> To = string.Empty;

    [AutoNetworkedField]
    public ProtoId<FactionRelationsPrototype> Relation = string.Empty;
}
