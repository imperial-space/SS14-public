using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

/// <summary>Общие кирпичики для окон консолей СБ.</summary>
internal static class SecurityUi
{
    public static BoxContainer Column(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    public static BoxContainer Row(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    public static Label Header(string text)
    {
        return new Label { Text = text, StyleClasses = { "LabelHeading" } };
    }

    public static BoxContainer Field(string name, string value)
    {
        var row = Row();
        row.AddChild(new Label { Text = name + ":", MinWidth = 140 });
        row.AddChild(new Label { Text = value, HorizontalExpand = true, ClipText = true });
        return row;
    }

    public static Button Button(string text, Action onPressed, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled };
        button.OnPressed += _ => onPressed();
        return button;
    }

    /// <summary>
    /// Окно перестраивается раз в секунду; чтобы набранный текст и фокус не пропадали,
    /// поля ввода переиспользуются, а фокус возвращается после перестройки.
    /// </summary>
    public static LineEdit? FocusedEdit(IEnumerable<LineEdit> edits)
    {
        foreach (var edit in edits)
        {
            if (edit.HasKeyboardFocus())
                return edit;
        }

        return null;
    }

    public static void RestoreFocus(LineEdit? edit, int cursor)
    {
        if (edit == null)
            return;

        edit.GrabKeyboardFocus();
        edit.CursorPosition = Math.Min(cursor, edit.Text.Length);
    }

    public static DefaultWindow Window(string title, Vector2 size, out BoxContainer body)
    {
        var window = new DefaultWindow { Title = title, MinSize = size, SetSize = size };
        body = Column(6);
        window.Contents.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { body } });
        return window;
    }
}
