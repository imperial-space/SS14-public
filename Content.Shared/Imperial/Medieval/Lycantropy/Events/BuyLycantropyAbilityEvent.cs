using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed partial class BuyLycantropyAbilityEvent : EntityEventArgs
{
    public readonly ProtoId<LycantropyAbilityPrototype> Proto;
    public readonly NetEntity Ent;

    public BuyLycantropyAbilityEvent(NetEntity ent, ProtoId<LycantropyAbilityPrototype> proto)
    {
        Ent = ent;
        Proto = proto;
    }
}
