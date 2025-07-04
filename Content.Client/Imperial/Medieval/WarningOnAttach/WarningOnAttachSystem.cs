using Content.Shared.Imperial.Medieval.WarningOnAttach;

namespace Content.Client.Imperial.Medieval.WarningOnAttach;

public sealed class WarningOnAttachSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ShowWarningOnAttachMessage>(OnShowWarningOnAttachMessage);
    }

    private void OnShowWarningOnAttachMessage(ShowWarningOnAttachMessage ev)
    {
        var window = new AttachWarningWindow(ev.Message);
        window.OpenCentered();
    }
}
