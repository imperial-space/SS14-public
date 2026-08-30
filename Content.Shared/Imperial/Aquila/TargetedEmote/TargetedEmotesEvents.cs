using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Aquila.TargetedEmote;

[Serializable, NetSerializable]
public sealed class PlayTargetedEmoteMessage(ProtoId<TargetedEmotePrototype> protoId, NetEntity target) : EntityEventArgs
{
    public readonly ProtoId<TargetedEmotePrototype> ProtoId = protoId;
    public readonly NetEntity Target = target;
}
