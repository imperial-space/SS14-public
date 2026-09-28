using Content.Shared.Imperial.Heretic;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticRitualBui : BoundUserInterface
{
    private HereticRitualWindow? _window;

    public HereticRitualBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        EnsureWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticRitualBuiState ritualState)
            return;

        EnsureWindow();
        _window?.Populate(ritualState);
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = this.CreateWindow<HereticRitualWindow>();
        _window.OnRitualSelected  += id     => SendMessage(new HereticSelectRitualMessage  { RitualId = id });
        _window.OnOfferingSelected += target => SendMessage(new HereticSelectOfferingMessage { Target   = target });
    }
}
