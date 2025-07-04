using Content.Shared.Friends.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Friends.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FactionDataContainerComponent : Component
{
    /// <summary>
    /// Участники и информация о них.
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<int, FactionMemberData>> CachedMembers = new();

    /// <summary>
    /// Цели для каждой из групп каждой из фракций
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<FactionMemberGroup, string>> Objectives = new();

    /// <summary>
    /// Дипломатические отношения между фракциями
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>>> Relations = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<FactionMemberGroup, List<EntityUid>>> Points = new();
}

