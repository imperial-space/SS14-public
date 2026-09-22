using Content.Shared.Imperial.Heretic;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticFleshWeaveOrganBui : BoundUserInterface
{
    private HereticFleshWeaveOrganWindow? _window;

    public HereticFleshWeaveOrganBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        EnsureWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticFleshWeaveOrganBuiState organState)
            return;

        EnsureWindow();
        _window?.Populate(organState);
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = this.CreateWindow<HereticFleshWeaveOrganWindow>();
        _window.OnOrganSelected += (organ, target) =>
            SendMessage(new HereticFleshWeaveSelectOrganMessage { Organ = organ, Target = target });
    }
}
