using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Factions.Components;

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

    /// <summary>
    /// Цели фракций
    /// </summary>
    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, List<FactionGoalData>> Goals = new();

    /// <summary>
    /// Счётчик смертей для каждой фракции
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, int> Deaths = new();

    /// <summary>
    /// Счётчик казней для каждой фракции
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<ProtoId<MedievalFactionPrototype>, int>> Executions = new();

    /// <summary>
    /// Счётчик убийства мобов для каждой фракции
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<string, int>> MobKills = new();

    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<FactionMemberGroup, List<EntityUid>>> Points = new();
}

