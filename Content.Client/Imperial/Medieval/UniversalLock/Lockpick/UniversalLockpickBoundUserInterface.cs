using Content.Shared.Imperial.Medieval.UniversalSecurity;

namespace Imperial.Medieval.UniversalLock.Lockpick;

public sealed class UniversalLockpickBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private UniversalLockpickWindow? _window;

    public UniversalLockpickBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new UniversalLockpickWindow();

        _window.OpenCentered();

        _window.OnClose += Close;

        _window.OnSetCode += code => SendMessage(new UniversalLockpickSetCodeMessage(code));
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not UniversalLockpickBuiState lockpickState)
            return;

        _window?.UpdateState(lockpickState.MaxValue, lockpickState.Length, lockpickState.CodeState);
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
