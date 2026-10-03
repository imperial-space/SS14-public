using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Плюшевый Карп-сие: капеллан, использовав его в руке, становится другом всех космических карпов.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NullRodFactionGranterComponent : Component
{
    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction;

    [DataField]
    public LocId GrantMessage = "null-rod-carp-blessing";

    [DataField, AutoNetworkedField]
    public bool Used;
}
