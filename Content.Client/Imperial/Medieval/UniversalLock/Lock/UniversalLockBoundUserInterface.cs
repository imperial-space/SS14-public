using Content.Shared.Imperial.Medieval.UniversalSecurity;

namespace Imperial.Medieval.UniversalLock.Lock;

public sealed class UniversalLockBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private UniversalLockWindow? _window;

    public UniversalLockBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new UniversalLockWindow();

        _window.OpenCentered();

        // Теперь передаем в сообщение и имя, и код
        _window.OnSetCode += (name, code, maxValue) => SendMessage(new UniversalLockSetCodeMessage(name, code, maxValue));
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not UniversalLockBuiState lockState)
            return;

        _window?.UpdateState(lockState.MaxValue, lockState.Length);
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
