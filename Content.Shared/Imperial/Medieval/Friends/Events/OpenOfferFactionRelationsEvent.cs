using Content.Shared.Friends.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

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
