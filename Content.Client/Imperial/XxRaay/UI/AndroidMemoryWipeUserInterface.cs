using System;
using Content.Shared.Imperial.XxRaay.Android;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Robust.Shared.ViewVariables;

namespace Content.Client.Imperial.XxRaay.UI;

/// <summary>
/// Окно уведомления об обнулении памяти
/// </summary>
public sealed class AndroidMemoryWipeWindow : DefaultWindow
{
    public event Action? ConfirmAndClosePressed;

    private readonly CheckBox _confirmCheckBox;
    private readonly Button _closeButton;

    public AndroidMemoryWipeWindow()
    {
        Title = Loc.GetString("android-memorywipe-window-title");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 4
        };

        ContentsContainer.AddChild(root);

        var description = new Label
        {
            Text = Loc.GetString("android-memorywipe-window-text")
        };
        root.AddChild(description);

        _confirmCheckBox = new CheckBox
        {
            Text = Loc.GetString("android-memorywipe-window-checkbox")
        };
        root.AddChild(_confirmCheckBox);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 4
        };

        _closeButton = new Button
        {
            Text = Loc.GetString("android-memorywipe-window-close"),
            Disabled = true
        };
        _closeButton.OnPressed += _ => ConfirmAndClosePressed?.Invoke();

        _confirmCheckBox.OnToggled += args =>
        {
            _closeButton.Disabled = !args.Pressed;
        };

        buttons.AddChild(_closeButton);
        root.AddChild(buttons);
    }
}

[UsedImplicitly]
public sealed class AndroidMemoryWipeUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AndroidMemoryWipeWindow? _window;

    public AndroidMemoryWipeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AndroidMemoryWipeWindow>();
        _window.ConfirmAndClosePressed += OnConfirmAndClosePressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
    }

    private void OnConfirmAndClosePressed()
    {
        SendMessage(new AndroidMemoryWipeAcknowledgeMessage(true));
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.ConfirmAndClosePressed -= OnConfirmAndClosePressed;
            _window = null;
        }

        base.Dispose(disposing);
    }
}

