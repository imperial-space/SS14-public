using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

public sealed class CultBloodRitesBui : BoundUserInterface
{
    [ViewVariables]
    private CultBloodRitesWindow? _window;

    public CultBloodRitesBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CultBloodRitesWindow>();
        _window.OnModeSelected += mode =>
        {
            SendMessage(new CultBloodRitesChoiceMessage(mode));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CultBloodRitesBuiState bloodState || _window == null)
            return;

        _window.SetState(bloodState.SelectedMode, bloodState.Charges);
    }
}
