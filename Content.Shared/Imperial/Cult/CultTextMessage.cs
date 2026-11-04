using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed class CultTextMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}
