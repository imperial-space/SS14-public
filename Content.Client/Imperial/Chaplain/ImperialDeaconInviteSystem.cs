using Content.Shared.Imperial.Chaplain;

namespace Content.Client.Imperial.Chaplain;

public sealed class ImperialDeaconInviteSystem : EntitySystem
{
    private ImperialDeaconInviteWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ImperialDeaconInviteEvent>(OnInvite);
    }

    private void OnInvite(ImperialDeaconInviteEvent ev)
    {
        _window?.Close();

        var window = new ImperialDeaconInviteWindow(ev.Deity);
        window.OnAnswer += accepted => RaiseNetworkEvent(new ImperialDeaconInviteResponseEvent(accepted));
        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };

        _window = window;
        window.OpenCentered();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}
