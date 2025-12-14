using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Factions;

public struct FactionMenuData
{
    public ProtoId<MedievalFactionPrototype> Faction;
    public Dictionary<int, FactionMemberData> Members;
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>>> Relations;
    public FactionMenuAccess Access;
    public FactionMemberGroup SelfGroup;
    public List<FactionGoalData> Goals;
    public int Self;

    public FactionMenuData(ProtoId<MedievalFactionPrototype> faction, Dictionary<int, FactionMemberData> members,
        Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>>> relations,
        FactionMenuAccess access, FactionMemberGroup selfGroup, List<FactionGoalData> goals, int self)
    {
        Faction = faction;
        Members = members;
        Relations = relations;
        Access = access;
        SelfGroup = selfGroup;
        Goals = goals;
        Self = self;
    }
}
