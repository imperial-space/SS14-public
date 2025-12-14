using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

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
