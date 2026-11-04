using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.UI;

/// <summary>
/// Knob из tgui-core: круглая ручка 2.6em × size, дуга 270° с заливкой цвета,
/// курсор поворачивается от −135° до +135°, значение всплывает при перетаскивании.
/// </summary>
public sealed class TguiKnob : TguiDraggable
{
    public Color FillColor = Tgui.PrimaryLine;
    private readonly float _size;

    public TguiKnob(float size = 1)
    {
        _size = size;
        var px = 12 * size * 2.6f;
        MinSize = new Vector2(px, px);
        SetSize = new Vector2(px, px);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = UIScale;
        var diameter = MathF.Min(PixelSize.X, PixelSize.Y);
        var center = PixelSize / 2;
        var em = 12 * _size * scale;
        var ratio = Ratio(DisplayValue);

        // Кольцо: трек и заливка (svg r=50, stroke 8 из 100).
        var ringRadius = diameter / 2 - diameter * 0.04f;
        var ringWidth = diameter * 0.08f;
        TguiDraw.Arc(handle, center, ringRadius, ringWidth, 135, 405, Color.White.WithAlpha(0.1f));
        if (ratio > 0)
            TguiDraw.Arc(handle, center, ringRadius, ringWidth, 135, 135 + 270 * ratio, FillColor);

        // Корпус ручки с бликом сверху (Knob__circle, inset 0.1em от кольца).
        var bodyRadius = ringRadius - ringWidth / 2 - 0.1f * em;
        handle.DrawCircle(center, bodyRadius, Color.FromHex("#2f2f2f"));
        handle.DrawCircle(center - new Vector2(0, bodyRadius * 0.15f), bodyRadius * 0.85f, Color.White.WithAlpha(0.05f));
        handle.DrawCircle(center, bodyRadius, Color.Black.WithAlpha(0.6f), false);

        // Курсор: светлая риска у края корпуса.
        var angle = MathF.PI / 180 * (-135 + 270 * ratio);
        var dir = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var normal = new Vector2(-dir.Y, dir.X);
        var outer = center + dir * (bodyRadius - 0.05f * em);
        var inner = center + dir * (bodyRadius - 0.85f * em);
        var half = 0.1f * em;
        TguiDraw.Quad(handle, inner - normal * half, inner + normal * half, outer + normal * half, outer - normal * half,
            Color.White.WithAlpha(0.9f));

        if (!Dragging)
            return;

        // Knob__popupValue над ручкой.
        var font = Tgui.Font(12);
        var text = FormatValue(DisplayValue);
        var dims = handle.GetDimensions(font, text, scale);
        var box = UIBox2.FromDimensions(new Vector2(center.X - dims.X / 2 - 4 * scale, -dims.Y - 6 * scale),
            dims + new Vector2(8, 4) * scale);
        handle.DrawRect(box, Color.Black.WithAlpha(0.8f));
        handle.DrawString(font, box.TopLeft + new Vector2(4, 2) * scale, text, scale, Color.White);
    }
}
