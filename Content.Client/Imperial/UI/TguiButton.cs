using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.UI;

/// <summary>Плоская кнопка tgui: заливка цветом, светлее при наведении, серая при отключении.</summary>
public sealed class TguiButton : BaseButton
{
    private readonly PanelContainer _panel;
    private readonly StyleBoxFlat _style;
    private Color _color;

    public readonly Label TextLabel;
    private readonly Label _icon;
    private readonly int _fontSize;

    public TguiButton(string text, Color color, int fontSize = 12, bool bold = false)
    {
        _fontSize = fontSize;
        _color = color;
        _style = new StyleBoxFlat { BackgroundColor = color, ContentMarginLeftOverride = 6, ContentMarginRightOverride = 6 };
        TextLabel = new Label
        {
            Text = text,
            FontOverride = Tgui.Font(fontSize, bold),
            FontColorOverride = Tgui.TextColor,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(4, 2),
        };
        _icon = new Label { Visible = false, VerticalAlignment = VAlignment.Center, Margin = new Thickness(4, 2, 0, 2) };
        var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Children = { _icon, TextLabel } };
        TextLabel.HorizontalExpand = true;
        _panel = new PanelContainer { PanelOverride = _style, Children = { content } };
        AddChild(_panel);
        MinHeight = 22;
        MouseFilter = MouseFilterMode.Stop;
        DrawModeChanged();
    }

    /// <summary>Иконка Font Awesome слева от текста, как у Button icon=... в tgui.</summary>
    public void SetIcon(string? icon)
    {
        if (icon == null || !TguiIcons.TryGet(icon, out var glyph, out var regular))
        {
            _icon.Visible = false;
            return;
        }

        _icon.Text = glyph;
        _icon.FontOverride = TguiIcons.Font(_fontSize, regular);
        _icon.Visible = true;
        TextLabel.Visible = !string.IsNullOrEmpty(TextLabel.Text);
        DrawModeChanged();
    }

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            DrawModeChanged();
        }
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // Базовый конструктор вызывает это до инициализации полей.
        if (_style == null || TextLabel == null)
            return;

        var transparent = _color.A <= 0.01f;
        _style.BackgroundColor = DrawMode switch
        {
            DrawModeEnum.Disabled => transparent ? Color.Transparent : Tgui.Disabled,
            DrawModeEnum.Hover => transparent ? Color.White.WithAlpha(0.1f) : Tgui.Lighten(_color, 0.08f),
            DrawModeEnum.Pressed => transparent ? Color.White.WithAlpha(0.18f) : Tgui.Lighten(_color, 0.15f),
            _ => _color,
        };
        TextLabel.FontColorOverride = DrawMode == DrawModeEnum.Disabled
            ? Color.FromHex(transparent ? "#888888" : "#bbbbbb")
            : Tgui.TextColor;
        if (_icon != null)
            _icon.FontColorOverride = TextLabel.FontColorOverride;
    }
}
