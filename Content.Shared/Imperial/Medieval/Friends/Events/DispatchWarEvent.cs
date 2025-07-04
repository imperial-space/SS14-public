using Content.Shared.Friends.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Friends;

[Serializable, NetSerializable]
public sealed partial class DispatchWarEvent : EntityEventArgs
{
    public ProtoId<MedievalFactionPrototype> UserFaction;
    public ProtoId<MedievalFactionPrototype> TargetFaction;

    public DispatchWarEvent(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> targetFaction)
    {
        UserFaction = userFaction;
        TargetFaction = targetFaction;
    }
}
