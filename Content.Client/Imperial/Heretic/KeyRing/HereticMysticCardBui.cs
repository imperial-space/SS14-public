using Content.Shared.Imperial.Heretic.KeyRing;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Heretic.KeyRing;

public sealed class HereticMysticCardBui : BoundUserInterface
{
    private HereticMysticCardWindow? _window;

    public HereticMysticCardBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        EnsureWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticMysticCardBuiState cardState) return;
        EnsureWindow();
        _window?.Populate(cardState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void EnsureWindow()
    {
        if (_window != null) return;
        _window = this.CreateWindow<HereticMysticCardWindow>();
        _window.OnCardSelected += name => SendMessage(new HereticMysticCardSelectMessage(name));
    }
}
