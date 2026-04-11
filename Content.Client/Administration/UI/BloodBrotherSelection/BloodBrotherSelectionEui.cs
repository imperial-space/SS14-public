using Content.Client.Eui;
using Content.Shared.Administration;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.Administration.UI.BloodBrotherSelection;

[UsedImplicitly]
public sealed class BloodBrotherSelectionEui : BaseEui
{
    private readonly BloodBrotherSelectionWindow _window;

    public BloodBrotherSelectionEui()
    {
        _window = new BloodBrotherSelectionWindow();
        _window.OnConfirmed += userId =>
        {
            SendMessage(new BloodBrotherSelectionChoiceMessage(userId));
            _window.Close();
        };
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is BloodBrotherSelectionEuiState selectionState)
            _window.SetState(selectionState);
    }
}