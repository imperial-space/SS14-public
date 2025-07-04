using Content.Shared.Friends;
using Content.Shared.Friends.Components;
using Content.Shared.Friends.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client.Friends;

public struct FactionMenuData
{
    public ProtoId<MedievalFactionPrototype> Faction;
    public Dictionary<int, FactionMemberData> Members;
    public Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>>> Relations;
    public FactionMenuAccess Access;
    public FactionMemberGroup SelfGroup;
    public int Self;

    public FactionMenuData(ProtoId<MedievalFactionPrototype> faction, Dictionary<int, FactionMemberData> members,
        Dictionary<ProtoId<MedievalFactionPrototype>, Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>>> relations,
        FactionMenuAccess access, FactionMemberGroup selfGroup, int self)
    {
        Faction = faction;
        Members = members;
        Relations = relations;
        Access = access;
        SelfGroup = selfGroup;
        Self = self;
    }
}
