using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed partial class OpenOfferFactionRelationsEvent : EntityEventArgs
{
    public NetEntity Target;
    public ProtoId<MedievalFactionPrototype> UserFaction;
    public ProtoId<MedievalFactionPrototype> TargetFaction;

    public OpenOfferFactionRelationsEvent(NetEntity target, ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction)
    {
        Target = target;
        UserFaction = userFaction;
        TargetFaction = targetFaction;
    }
}
