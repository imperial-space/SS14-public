using System.Numerics;
using Content.Client.Imperial.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Bitrunning;

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
