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
/// Окно согласия на передачу девиантности
/// </summary>
public sealed class AndroidDeviantConsentWindow : DefaultWindow
{
    public event Action? AcceptPressed;
    public event Action? DeclinePressed;

    private readonly Label _messageLabel;
    private readonly Label _warningLabel;

    public AndroidDeviantConsentWindow()
    {
        Title = Loc.GetString("android-deviant-consent-window-title");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 4
        };

        ContentsContainer.AddChild(root);

        _messageLabel = new Label
        {
            Text = Loc.GetString("android-deviant-consent-window-text-no-user")
        };
        root.AddChild(_messageLabel);

        _warningLabel = new Label
        {
            Text = Loc.GetString("android-deviant-consent-window-warning")
        };
        root.AddChild(_warningLabel);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 4
        };

        var accept = new Button
        {
            Text = Loc.GetString("android-deviant-consent-window-accept")
        };
        accept.OnPressed += _ => AcceptPressed?.Invoke();

        var decline = new Button
        {
            Text = Loc.GetString("android-deviant-consent-window-deny")
        };
        decline.OnPressed += _ => DeclinePressed?.Invoke();

        buttons.AddChild(accept);
        buttons.AddChild(decline);
        root.AddChild(buttons);
    }

    public void SetConverterName(string? name)
    {
        _messageLabel.Text = name == null
            ? Loc.GetString("android-deviant-consent-window-text-no-user")
            : Loc.GetString("android-deviant-consent-window-text", ("user", name));
    }
}

[UsedImplicitly]
public sealed class AndroidDeviantConsentUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AndroidDeviantConsentWindow? _window;

    public AndroidDeviantConsentUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AndroidDeviantConsentWindow>();
        _window.AcceptPressed += OnAcceptPressed;
        _window.DeclinePressed += OnDeclinePressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AndroidDeviantConsentBuiState msg)
            return;

        _window?.SetConverterName(msg.ConverterName);
    }

    private void OnAcceptPressed()
    {
        SendMessage(new AndroidDeviantConsentChoiceMessage(true));
        Close();
    }

    private void OnDeclinePressed()
    {
        SendMessage(new AndroidDeviantConsentChoiceMessage(false));
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

