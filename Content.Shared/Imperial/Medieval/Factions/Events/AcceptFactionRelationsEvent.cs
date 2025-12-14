using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed partial class AcceptFactionRelationsEvent : EntityEventArgs
{
    public ProtoId<MedievalFactionPrototype> UserFaction;
    public ProtoId<MedievalFactionPrototype> TargetFaction;
    public ProtoId<FactionRelationsPrototype> Relation;

    public AcceptFactionRelationsEvent(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        UserFaction = userFaction;
        TargetFaction = targetFaction;
        Relation = relation;
    }
}
