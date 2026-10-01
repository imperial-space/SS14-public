using Content.Shared.Imperial.Chaplain;

namespace Content.Client.Imperial.Chaplain;

public sealed class ImperialReligionSetupSystem : EntitySystem
{
    private ImperialReligionSetupWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ImperialReligionSetupOpenEvent>(OnOpen);
    }

    private void OnOpen(ImperialReligionSetupOpenEvent ev)
    {
        _window?.Close();

        var window = new ImperialReligionSetupWindow(ev.Religion, ev.Deity, ev.Bible);
        window.OnSubmit += (religion, deity, bible) =>
            RaiseNetworkEvent(new ImperialReligionSetupSubmitEvent(religion, deity, bible));
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
