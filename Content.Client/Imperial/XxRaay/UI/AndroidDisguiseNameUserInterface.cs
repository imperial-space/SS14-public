using System;
using Content.Shared.Imperial.XxRaay.Android;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Robust.Shared.ViewVariables;

namespace Content.Client.Imperial.XxRaay.UI;

public sealed class AndroidDisguiseNameWindow : DefaultWindow
{
    public event Action<string>? AcceptPressed;
    public event Action? DeclinePressed;

    private readonly LineEdit _nameEdit;

    public AndroidDisguiseNameWindow()
    {
        Title = Loc.GetString("android-disguise-name-title");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 4
        };

        ContentsContainer.AddChild(root);

        var description = new Label
        {
            Text = Loc.GetString("android-disguise-name-text")
        };
        root.AddChild(description);

        _nameEdit = new LineEdit();
        root.AddChild(_nameEdit);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 4
        };

        var accept = new Button
        {
            Text = Loc.GetString("android-disguise-name-accept")
        };
        accept.OnPressed += _ => AcceptPressed?.Invoke(_nameEdit.Text);

        var decline = new Button
        {
            Text = Loc.GetString("android-disguise-name-decline")
        };
        decline.OnPressed += _ => DeclinePressed?.Invoke();

        buttons.AddChild(accept);
        buttons.AddChild(decline);
        root.AddChild(buttons);
    }
}

[UsedImplicitly]
public sealed class AndroidDisguiseNameUserInterface : BoundUserInterface
{
    [ViewVariables]
    private AndroidDisguiseNameWindow? _window;

    public AndroidDisguiseNameUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AndroidDisguiseNameWindow>();
        _window.AcceptPressed += OnAcceptPressed;
        _window.DeclinePressed += OnDeclinePressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
    }

    private void OnAcceptPressed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        SendMessage(new AndroidDisguiseNameChosenMessage(name));
        Close();
    }

    private void OnDeclinePressed()
    {
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

