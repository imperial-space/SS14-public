using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно Общения культа — ввод текста сообщения.
/// </summary>
public sealed class CultCommuneWindow : DefaultWindow
{
    public event Action<string>? OnMessageSubmit;

    private readonly LineEdit _input;

    public CultCommuneWindow()
    {
        Title = Loc.GetString("cult-commune-window-title");
        MinSize = new Vector2(320, 120);

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            SeparationOverride = 6,
        };

        _input = new LineEdit
        {
            PlaceHolder = Loc.GetString("cult-commune-placeholder"),
            HorizontalExpand = true,
            MinSize = new Vector2(0, 32),
        };

        _input.OnTextEntered += args =>
        {
            Submit(args.Text);
        };

        var confirmBtn = new Button
        {
            Text = Loc.GetString("cult-commune-submit"),
            HorizontalExpand = true,
            MinSize = new Vector2(0, 32),
        };

        confirmBtn.OnPressed += _ =>
        {
            Submit(_input.Text);
        };

        vbox.AddChild(_input);
        vbox.AddChild(confirmBtn);
        Contents.AddChild(vbox);
    }

    protected override void Opened()
    {
        base.Opened();
        _input.GrabKeyboardFocus();
    }

    private void Submit(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return;
        OnMessageSubmit?.Invoke(text);
        Close();
    }
}
