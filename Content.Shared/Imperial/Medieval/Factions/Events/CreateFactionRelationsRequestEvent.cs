using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed partial class CreateFactionRelationsRequestEvent : EntityEventArgs
{
    public NetEntity Target;
    public ProtoId<MedievalFactionPrototype> UserFaction;
    public ProtoId<MedievalFactionPrototype> TargetFaction;
    public ProtoId<FactionRelationsPrototype> Relation;

    public CreateFactionRelationsRequestEvent(NetEntity target, ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction, ProtoId<FactionRelationsPrototype> relation)
    {
        Target = target;
        UserFaction = userFaction;
        TargetFaction = targetFaction;
        Relation = relation;
    }
}
