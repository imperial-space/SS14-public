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
/// RoundGauge из tgui-core: полукруг 2.6em × size, цветные дуги диапазонов, стрелка,
/// мигающий значок тревоги и значение справа.
/// </summary>
public sealed class TguiRoundGauge : Control
{
    private readonly float _size;
    private readonly IGameTiming _timing;

    public float MinValue;
    public float MaxValue = 1;
    public float Value;
    public float? AlertAfter;
    public float? AlertBefore;
    public Func<float, string> Format = v => v.ToString("0.##", CultureInfo.InvariantCulture);
    public (string Color, float From, float To)[] Ranges = Array.Empty<(string, float, float)>();

    public TguiRoundGauge(float size = 1)
    {
        _size = size;
        _timing = IoCManager.Resolve<IGameTiming>();
        var em = 12 * size;
        MinSize = new Vector2(em * 2.6f + 60, em * 1.3f + 8);
    }

    private float Scaled(float value)
    {
        return MaxValue <= MinValue ? 0 : Math.Clamp((value - MinValue) / (MaxValue - MinValue), 0, 1);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        var em = 12 * _size * scale;
        var width = em * 2.6f;
        var radius = width / 2 - width * 0.05f;
        var stroke = width * 0.1f;
        var center = new Vector2(width / 2, em * 1.3f + 2 * scale);

        // Трек и дуги диапазонов (от 180° слева до 360° справа, то есть верхняя половина).
        TguiDraw.Arc(handle, center, radius, stroke, -90, 90, Color.White.WithAlpha(0.1f));
        foreach (var (color, from, to) in Ranges)
        {
            var a = -90 + 180 * Scaled(from);
            var b = -90 + 180 * Scaled(to);
            if (b > a)
                TguiDraw.Arc(handle, center, radius, stroke, a, b, TguiColors.Get(color));
        }

        // Стрелка.
        var angle = MathF.PI / 180 * (-90 + 180 * Scaled(Value));
        var dir = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var normal = new Vector2(-dir.Y, dir.X);
        var tip = center + dir * (radius - stroke * 0.2f);
        TguiDraw.Triangle(handle, center + normal * stroke * 0.35f, center - normal * stroke * 0.35f, tip, Color.White.WithAlpha(0.85f));
        handle.DrawCircle(center, stroke * 0.45f, Color.FromHex("#888888"));

        // Тревога.
        var alert = AlertAfter is { } after && Value > after || AlertBefore is { } before && Value < before;
        if (alert && _timing.RealTime.TotalSeconds % 1 < 0.6)
        {
            var color = CurrentColor();
            TguiIcons.DrawGlyph(handle, "exclamation-triangle", (int) MathF.Max(8, 6 * _size), center - new Vector2(0, radius * 0.45f), color, scale);
        }

        var font = Tgui.Font(12);
        var text = Format(Value);
        var dims = handle.GetDimensions(font, text, scale);
        handle.DrawString(font, new Vector2(width + 4 * scale, center.Y - dims.Y), text, scale, Tgui.TextColor);
    }

    private Color CurrentColor()
    {
        foreach (var (color, from, to) in Ranges)
        {
            if (Value >= from && Value <= to)
                return TguiColors.Get(color);
        }

        return Tgui.Bad;
    }
}
