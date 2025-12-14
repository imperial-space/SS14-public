using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed partial class OpenFactionRelationsRequestEvent : EntityEventArgs
{
    public NetEntity Target;
    public ProtoId<MedievalFactionPrototype> From;

    public OpenFactionRelationsRequestEvent(NetEntity target, ProtoId<MedievalFactionPrototype> from)
    {
        Target = target;
        From = from;
    }
}
