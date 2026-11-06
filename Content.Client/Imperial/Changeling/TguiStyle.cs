using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Content.Client.Message;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Changeling;

/// <summary>Цвета и элементы tgui-core (Section, NoticeBox, цвета кнопок) для окон генокрада.</summary>
public static class TguiStyle
{
    public static readonly Color WindowBackground = Color.FromHex("#1A1A1A");
    public static readonly Color SectionBackground = Color.FromHex("#00000054");
    public static readonly Color SectionTitleLine = Color.FromHex("#4972a1");
    public static readonly Color Good = Color.FromHex("#5baa27");
    public static readonly Color Average = Color.FromHex("#f29a29");
    public static readonly Color Bad = Color.FromHex("#db2828");
    public static readonly Color Purple = Color.FromHex("#a42b9d");
    public static readonly Color Default = Color.FromHex("#3e6189");
    public static readonly Color Disabled = Color.FromHex("#4c4c4c");
    public static readonly Color TabNormal = Color.FromHex("#00000033");
    public static readonly Color TabSelected = Color.FromHex("#ffffff20");
    public static readonly Color Label = Color.FromHex("#7e90a7");

    /// <summary>Кнопка tgui с цветом color; недоступная — серая, как disabled.</summary>
    public static void Paint(Button button, Color color)
    {
        button.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = button.Disabled ? Disabled : color,
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
        button.Modulate = button.Disabled ? Color.White.WithAlpha(0.6f) : Color.White;
    }

    /// <summary>&lt;Section title&gt;: заголовок с синей линией снизу и тёмное тело.</summary>
    public static PanelContainer Section(out BoxContainer titleRow, out BoxContainer body)
    {
        var panel = new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = SectionBackground } };
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        panel.AddChild(column);

        titleRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6, 4), MinHeight = 28 };
        column.AddChild(titleRow);
        column.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = SectionTitleLine },
            MinHeight = 2,
            HorizontalExpand = true,
        });

        body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), VerticalExpand = true };
        column.AddChild(body);
        return panel;
    }

    /// <summary>&lt;NoticeBox&gt; и &lt;NoticeBox danger&gt;.</summary>
    public static PanelContainer NoticeBox(string text, bool danger = false)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(danger ? "#9a2c2c" : "#bb9b68") },
        };
        var label = new RichTextLabel { Margin = new Thickness(8, 6) };
        label.SetMarkup($"[color={(danger ? "#ffffff" : "#000000")}]{text}[/color]");
        panel.AddChild(label);
        return panel;
    }

    /// <summary>Заголовок &lt;Section&gt;: белый жирный.</summary>
    public static RichTextLabel Title(string text)
    {
        var label = new RichTextLabel { VerticalAlignment = Control.VAlignment.Center };
        label.SetMarkup($"[bold][font size=14][color=#ffffff]{Robust.Shared.Utility.FormattedMessage.EscapeText(text)}[/color][/font][/bold]");
        return label;
    }

    /// <summary>&lt;Icon&gt; FontAwesome заменён текстурой.</summary>
    public static TextureRect Icon(Texture texture, float size, string? tooltip = null)
    {
        return new TextureRect
        {
            Texture = texture,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(size, size),
            VerticalAlignment = Control.VAlignment.Center,
            ToolTip = tooltip,
            MouseFilter = tooltip != null ? Control.MouseFilterMode.Stop : Control.MouseFilterMode.Ignore,
        };
    }

    /// <summary>bg-nanotrasen.svg: логотип NT водяным знаком.</summary>
    public static Control Watermark(Texture logo)
    {
        return new TextureRect
        {
            Texture = logo,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Modulate = Color.White.WithAlpha(0.06f),
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
    }
}
