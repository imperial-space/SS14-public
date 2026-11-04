using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Cult;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>tgui_input_text культа.</summary>
[UsedImplicitly]
public sealed class CultTextBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private bool _replacing;
    private bool _answered;

    public CultTextBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not CultTextState text)
            return;

        CloseWindow();
        _answered = false;

        _window = new DefaultWindow
        {
            Title = text.Title,
            MinSize = new Vector2(380, 110),
        };
        _window.OnClose += () =>
        {
            if (!_replacing && !_answered)
                Close();
        };

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(6) };
        box.AddChild(new Label { Text = text.Prompt });
        var edit = new LineEdit { HorizontalExpand = true };
        edit.IsValid = s => s.Length <= text.MaxLength;
        var ok = new Button { Text = Loc.GetString("cult-menu-ok"), HorizontalAlignment = Control.HAlignment.Right };

        void Submit()
        {
            if (_answered)
                return;
            _answered = true;
            SendMessage(new CultTextMessage(edit.Text));
            CloseWindow();
        }

        edit.OnTextEntered += _ => Submit();
        ok.OnPressed += _ => Submit();
        box.AddChild(edit);
        box.AddChild(ok);
        _window.Contents.AddChild(box);
        _window.OpenCentered();
        edit.GrabKeyboardFocus();
    }

    private void CloseWindow()
    {
        _replacing = true;
        _window?.Close();
        _window = null;
        _replacing = false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            CloseWindow();
    }
}
