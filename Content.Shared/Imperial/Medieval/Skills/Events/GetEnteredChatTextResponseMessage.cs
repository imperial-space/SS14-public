using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

[Serializable, NetSerializable]
public sealed class GetEnteredChatTextResponseMessage : EntityEventArgs
{
    public readonly string Text;
    public readonly NetEntity Target;
    public readonly NetEntity User;

    public GetEnteredChatTextResponseMessage(NetEntity target, NetEntity user, string text)
    {
        Target = target;
        User = user;
        Text = text;
    }
}
