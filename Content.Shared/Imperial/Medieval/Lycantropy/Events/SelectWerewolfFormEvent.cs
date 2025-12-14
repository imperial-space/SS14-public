using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed partial class SelectWerewolfFormEvent : EntityEventArgs
{
    public readonly ProtoId<PolymorphPrototype> Proto;
    public readonly NetEntity Ent;

    public SelectWerewolfFormEvent(NetEntity ent, ProtoId<PolymorphPrototype> proto)
    {
        Ent = ent;
        Proto = proto;
    }
}
