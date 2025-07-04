using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

[Serializable, NetSerializable]
public sealed class GetEnteredChatMessageMessage : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly NetEntity User;

    public GetEnteredChatMessageMessage(NetEntity target, NetEntity user)
    {
        Target = target;
        User = user;
    }
}
