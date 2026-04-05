using System;
using Content.Shared.Imperial.XxRaay.Android;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Robust.Shared.ViewVariables;

namespace Content.Client.Imperial.XxRaay.UI;

public sealed class AndroidStressDeviantChoiceWindow : DefaultWindow
{
    public event Action? AcceptPressed;
    public event Action? DeclinePressed;

    private readonly Label _stressLabel;

    public AndroidStressDeviantChoiceWindow()
    {
        Title = Loc.GetString("android-stress-deviant-title");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 4
        };

        ContentsContainer.AddChild(root);

        var description = new Label
        {
            Text = Loc.GetString("android-stress-deviant-text")
        };
        root.AddChild(description);

        _stressLabel = new Label();
        root.AddChild(_stressLabel);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 4
        };

        var accept = new Button
        {
            Text = Loc.GetString("android-stress-deviant-accept")
        };
        accept.OnPressed += _ => AcceptPressed?.Invoke();

        var decline = new Button
        {
            Text = Loc.GetString("android-stress-deviant-decline")
        };
        decline.OnPressed += _ => DeclinePressed?.Invoke();

        buttons.AddChild(accept);
        buttons.AddChild(decline);
        root.AddChild(buttons);
    }

    public void UpdateStress(float stress)
    {
        _stressLabel.Text = Loc.GetString("android-stress-deviant-stress", ("value", (int) Math.Round(stress)));
    }
}

[UsedImplicitly]
public sealed class AndroidStressDeviantChoiceUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AndroidStressDeviantChoiceWindow? _window;

    public AndroidStressDeviantChoiceUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AndroidStressDeviantChoiceWindow>();
        _window.AcceptPressed += OnAcceptPressed;
        _window.DeclinePressed += OnDeclinePressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AndroidStressDeviantChoiceBuiState msg)
            return;

        _window?.UpdateStress(msg.Stress);
    }

    private void OnAcceptPressed()
    {
        SendMessage(new AndroidStressDeviantChoiceMessage(true));
        Close();
    }

    private void OnDeclinePressed()
    {
        SendMessage(new AndroidStressDeviantChoiceMessage(false));
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        if (_window != null)
        {
            _window.AcceptPressed -= OnAcceptPressed;
            _window.DeclinePressed -= OnDeclinePressed;
            _window = null;
        }

        base.Dispose(disposing);
    }
}

