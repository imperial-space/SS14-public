using System.Numerics;
using Content.Client.Imperial.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Bitrunning;

/// <summary>
/// Элементы tgui, которых нет в общей библиотеке: иконки Font Awesome интерфейсов битраннинга,
/// Button.Confirm, цветные вкладки Tabs, Icon spin и DisplayDetails консоли.
/// </summary>
public static class BitrunningTgui
{
    private static readonly Dictionary<string, int> Glyphs = new()
    {
        ["star"] = 0xf005,
        ["random"] = 0xf074,
        ["download"] = 0xf019,
        ["coins"] = 0xf51e,
        ["cubes"] = 0xf1b3,
        ["gem"] = 0xf3a5,
        ["ghost"] = 0xf6e2,
        ["skull"] = 0xf54c,
        ["chevron-down"] = 0xf078,
        ["chevron-right"] = 0xf054,
        ["broadcast-tower"] = 0xf519,
        ["sync"] = 0xf021,
        ["check"] = 0xf00c,
        ["tint"] = 0xf043,
        ["fire"] = 0xf06d,
        ["skull-crossbones"] = 0xf714,
        ["lungs"] = 0xf604,
        ["toolbox"] = 0xf552,
        ["cart-plus"] = 0xf217,
        ["dollar-sign"] = 0xf155,
        ["info"] = 0xf129,
        ["plus"] = 0xf067,
        ["minus"] = 0xf068,
        ["plane-departure"] = 0xf5b0,
        ["parachute-box"] = 0xf4cd,
        ["plane-arrival"] = 0xf5af,
        ["search-location"] = 0xf689,
        ["boxes"] = 0xf468,
        ["plug"] = 0xf1e6,
        ["id-badge"] = 0xf2c1,
    };

    public static string Glyph(string name)
    {
        return Glyphs.TryGetValue(name, out var code) ? char.ConvertFromUtf32(code) : string.Empty;
    }

    /// <summary>Icon из tgui-core.</summary>
    public static Label Icon(string name, Color? color = null, int size = 12)
    {
        return new Label
        {
            Text = Glyph(name),
            FontOverride = TguiIcons.Font(size),
            FontColorOverride = color ?? Tgui.TextColor,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    /// <summary>Button с icon=...: иконка слева от текста.</summary>
    public static TguiButton Button(string text, string? icon = null, Color? color = null, Action? onPressed = null,
        bool disabled = false, string? tooltip = null, bool fluid = false)
    {
        var button = Tgui.Button(text, onPressed, color, disabled, fluid, tooltip: tooltip);
        if (icon != null)
            AddIcon(button, icon);
        return button;
    }

    public static void AddIcon(TguiButton button, string icon)
    {
        if (button.TextLabel.Parent is not { } content)
            return;

        var label = new Label
        {
            Text = Glyph(icon),
            FontOverride = TguiIcons.Font(12),
            FontColorOverride = button.Disabled ? Color.FromHex("#bbbbbb") : Tgui.TextColor,
            VerticalAlignment = Control.VAlignment.Center,
            Margin = new Thickness(4, 2, string.IsNullOrEmpty(button.TextLabel.Text) ? 4 : 0, 2),
        };
        content.AddChild(label);
        label.SetPositionInParent(0);
        button.TextLabel.Visible = !string.IsNullOrEmpty(button.TextLabel.Text);
    }

    /// <summary>Button.Confirm: первый клик спрашивает «Подтвердить?», второй — выполняет.</summary>
    public static TguiButton Confirm(string text, Action onConfirm, bool disabled = false, string? tooltip = null)
    {
        var button = Tgui.Button(text, null, null, disabled, tooltip: tooltip);
        var armed = false;
        button.OnPressed += _ =>
        {
            if (!armed)
            {
                armed = true;
                button.TextLabel.Text = Loc.GetString("bitrunning-tgui-confirm");
                button.Color = Tgui.Bad;
                return;
            }

            armed = false;
            button.TextLabel.Text = text;
            button.Color = Tgui.Primary;
            onConfirm();
        };
        button.OnMouseExited += _ =>
        {
            if (!armed)
                return;

            armed = false;
            button.TextLabel.Text = text;
            button.Color = Tgui.Primary;
        };
        return button;
    }

    /// <summary>Stack.Divider: вертикальная полоса между колонками.</summary>
    public static Control VDivider()
    {
        return new PanelContainer
        {
            MinWidth = 2,
            Margin = new Thickness(6, 0),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.White.WithAlpha(0.1f) },
        };
    }

    public static Control HDivider()
    {
        return new PanelContainer
        {
            MinHeight = 2,
            Margin = new Thickness(0, 4),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.White.WithAlpha(0.1f) },
        };
    }
}

/// <summary>Tabs fluid с backgroundColor у каждой вкладки (QuantumConsole).</summary>
public sealed class BitrunningColoredTab : BaseButton
{
    private readonly Color _background;
    private bool _selected;

    public BitrunningColoredTab(string text, string? icon, Color background, Color textColor, bool selected, string? trailingIcon = null)
    {
        _background = background;
        _selected = selected;
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;

        var box = Tgui.HBox(6);
        box.HorizontalAlignment = HAlignment.Center;
        box.Margin = new Thickness(8, 4, 8, 6);
        if (icon != null)
            box.AddChild(BitrunningTgui.Icon(icon, textColor));
        box.AddChild(Tgui.Text(text, textColor));
        if (trailingIcon != null)
            box.AddChild(BitrunningTgui.Icon(trailingIcon, textColor));
        AddChild(box);
    }

    public bool Selected
    {
        get => _selected;
        set => _selected = value;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var rect = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        var alpha = _selected ? 1f : DrawMode == DrawModeEnum.Hover ? 0.85f : 0.6f;
        handle.DrawRect(rect, _background.WithAlpha(alpha));
        if (_selected)
            handle.DrawRect(new UIBox2(0, PixelSize.Y - 2 * UIScale, PixelSize.X, PixelSize.Y), Color.FromHex("#d4dfec"));
    }
}

/// <summary>Icon spin: глиф вращается вокруг центра.</summary>
public sealed class BitrunningSpinner : Control
{
    private readonly string _glyph;
    private readonly Color _color;
    private readonly int _size;
    private float _angle;

    public BitrunningSpinner(string icon, Color color, int size)
    {
        _glyph = BitrunningTgui.Glyph(icon);
        _color = color;
        _size = size;
        MinSize = new Vector2(size * 1.6f, size * 1.6f);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _angle += args.DeltaSeconds * MathF.Tau;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var font = TguiIcons.Font(_size);
        var dims = handle.GetDimensions(font, _glyph, UIScale);
        var center = PixelSize / 2f;
        var old = handle.GetTransform();
        handle.SetTransform(Matrix3Helpers.CreateTransform(center, new Angle(_angle)) * old);
        handle.DrawString(font, -dims / 2f, _glyph, UIScale, _color);
        handle.SetTransform(old);
    }
}
