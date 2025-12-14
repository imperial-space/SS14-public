using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed partial class AddPlaguePointsMessage : EntityEventArgs
{
    public readonly ProtoId<MedievalPlagueSymptomPrototype> Proto;
    public readonly int Points;
    public readonly NetEntity Ent;

    public AddPlaguePointsMessage(ProtoId<MedievalPlagueSymptomPrototype> proto, int points, NetEntity ent)
    {
        Proto = proto;
        Points = points;
        Ent = ent;
    }
}
