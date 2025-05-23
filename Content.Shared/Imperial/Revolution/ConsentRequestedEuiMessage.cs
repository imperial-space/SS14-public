using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Revolutionary;

[Serializable, NetSerializable]
public sealed class ConsentRequestedEuiMessage(bool isAccepted) : EuiMessageBase
{
    public readonly bool IsAccepted = isAccepted;
}
