using Content.Shared.Imperial.Medieval.UniversalSecurity;

namespace Imperial.Medieval.UniversalLock.Lock;

public sealed class UniversalKeyBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private UniversalKeyWindow? _window;

    public UniversalKeyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new UniversalKeyWindow();
        _window.OpenCentered();

        _window.OnSetCode += (name, code) => SendMessage(new UniversalKeySetCodeMessage(code, name));
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not UniversalKeyBuiState)
            return;

        _window?.UpdateState();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
        _window?.Dispose();
    }
}
