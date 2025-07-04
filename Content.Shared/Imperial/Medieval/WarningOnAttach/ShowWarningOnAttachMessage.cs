using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.WarningOnAttach;

[Serializable, NetSerializable]
public sealed class ShowWarningOnAttachMessage : EntityEventArgs
{
    public string Message { get; }

    public ShowWarningOnAttachMessage(string message)
    {
        Message = Loc.GetString(message);
    }
}
