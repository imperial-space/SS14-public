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
/// Иконки tgui (Font Awesome Free 6, SIL OFL 1.1 — Resources/Fonts/Imperial/FontAwesome).
/// Имена — как в tgui-интерфейсах SS13, включая старые имена FA4 (snowflake-o, sun-o и т.п.).
/// </summary>
public static class TguiIcons
{
    private const string SolidPath = "/Fonts/Imperial/FontAwesome/fa-solid-900.ttf";
    private const string RegularPath = "/Fonts/Imperial/FontAwesome/fa-regular-400.ttf";

    private static readonly Dictionary<string, (int Code, bool Regular)> Glyphs = new()
    {
        ["power-off"] = (0xf011, false),
        ["times"] = (0xf00d, false),
        ["question-circle"] = (0xf059, false),
        ["fire"] = (0xf06d, false),
        ["snowflake-o"] = (0xf2dc, true),
        ["snowflake"] = (0xf2dc, false),
        ["magnet"] = (0xf076, false),
        ["sun-o"] = (0xf185, true),
        ["fast-forward"] = (0xf050, false),
        ["fast-backward"] = (0xf049, false),
        ["undo"] = (0xf0e2, false),
        ["angle-up"] = (0xf106, false),
        ["angle-down"] = (0xf107, false),
        ["angle-double-up"] = (0xf102, false),
        ["angle-double-down"] = (0xf103, false),
        ["minus"] = (0xf068, false),
        ["window-minimize"] = (0xf2d1, false),
        ["arrow-up"] = (0xf062, false),
        ["arrow-down"] = (0xf063, false),
        ["thermometer-full"] = (0xf2c7, false),
        ["square-o"] = (0xf0c8, true),
        ["check-square-o"] = (0xf14a, true),
        ["chevron-up"] = (0xf077, false),
        ["chevron-down"] = (0xf078, false),
        ["chevron-right"] = (0xf054, false),
        ["wrench"] = (0xf0ad, false),
        ["atom"] = (0xf5d2, false),
        ["cubes"] = (0xf1b3, false),
        ["exclamation-triangle"] = (0xf071, false),
    };

    private static readonly Dictionary<(int, bool), Font> Cache = new();

    public static Font Font(int size, bool regular = false)
    {
        if (Cache.TryGetValue((size, regular), out var font))
            return font;

        var cache = IoCManager.Resolve<IResourceCache>();
        font = new VectorFont(cache.GetResource<FontResource>(regular ? RegularPath : SolidPath), size);
        Cache[(size, regular)] = font;
        return font;
    }

    public static bool TryGet(string name, out string glyph, out bool regular)
    {
        if (Glyphs.TryGetValue(name, out var entry))
        {
            glyph = char.ConvertFromUtf32(entry.Code);
            regular = entry.Regular;
            return true;
        }

        glyph = string.Empty;
        regular = false;
        return false;
    }

    /// <summary>Icon из tgui: глиф Font Awesome нужного размера и цвета.</summary>
    public static Label Icon(string name, int size = 12, Color? color = null)
    {
        TryGet(name, out var glyph, out var regular);
        return new Label
        {
            Text = glyph,
            FontOverride = Font(size, regular),
            FontColorOverride = color ?? Tgui.TextColor,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
        };
    }

    /// <summary>Icon.Stack: несколько глифов друг на друге.</summary>
    public static Control Stack(int size, Color color, params string[] names)
    {
        var layout = new LayoutContainer { MinSize = new Vector2(size * 1.25f, size * 1.25f) };
        foreach (var name in names)
        {
            var icon = Icon(name, size, color);
            layout.AddChild(icon);
            LayoutContainer.SetAnchorAndMarginPreset(icon, LayoutContainer.LayoutPreset.Wide);
        }

        return layout;
    }

    public static void DrawGlyph(DrawingHandleScreen handle, string name, int size, Vector2 center, Color color, float scale)
    {
        if (!TryGet(name, out var glyph, out var regular))
            return;

        var font = Font(size, regular);
        var dims = handle.GetDimensions(font, glyph, scale);
        handle.DrawString(font, center - dims / 2, glyph, scale, color);
    }
}

/// <summary>Цвета tgui по имени (ranges / color у ProgressBar, Knob, RoundGauge).</summary>
public static class TguiColors
{
    public static Color Get(string name)
    {
        return name switch
        {
            "good" => Tgui.Good,
            "average" => Tgui.Average,
            "bad" => Tgui.Bad,
            "teal" => Color.FromHex("#00b5ad"),
            "grey" => Color.FromHex("#646464"),
            "black" => Color.FromHex("#1a1a1a"),
            "yellow" => Tgui.Yellow,
            "orange" => Color.FromHex("#f2711c"),
            "blue" => Tgui.Blue,
            "label" => Tgui.Label,
            _ => Tgui.PrimaryLine,
        };
    }
}

/// <summary>
/// Общая логика DraggableControl из tgui-core: перетаскивание мышью по вертикали меняет значение на
/// step за каждые stepPixelSize пикселей, onChange — по отпусканию; клик без перетаскивания открывает ввод.
/// </summary>
public abstract class TguiDraggable : Control
{
    public float MinValue;
    public float MaxValue = 100;
    public float Step = 1;
    public float StepPixelSize = 1;
    public string Unit = string.Empty;
    public Func<float, string>? Format;

    /// <summary>onChange: значение после перетаскивания или ввода.</summary>
    public event Action<float>? OnChange;

    protected bool Dragging;
    protected float DisplayValue;
    private float _value;
    private float _dragStartValue;
    private Vector2 _dragStart;
    private bool _moved;
    private readonly LineEdit _input;

    protected TguiDraggable()
    {
        MouseFilter = MouseFilterMode.Stop;
        _input = new LineEdit { Visible = false, HorizontalExpand = true, MinWidth = 60 };
        _input.OnTextEntered += args => CommitInput(args.Text);
        _input.OnFocusExit += _ => _input.Visible = false;
        AddChild(_input);
    }

    public float Value
    {
        get => _value;
        set
        {
            _value = value;
            if (!Dragging)
                DisplayValue = value;
        }
    }

    public string FormatValue(float value)
    {
        if (Format != null)
            return Format(value);

        var text = value.ToString("0.##", CultureInfo.InvariantCulture);
        return Unit.Length > 0 ? $"{text} {Unit}" : text;
    }

    protected float Ratio(float value)
    {
        return MaxValue <= MinValue ? 0 : Math.Clamp((value - MinValue) / (MaxValue - MinValue), 0, 1);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || _input.Visible)
            return;

        Dragging = true;
        _moved = false;
        _dragStart = args.PointerLocation.Position;
        _dragStartValue = _value;
        DisplayValue = _value;
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!Dragging)
            return;

        var offset = (_dragStart.Y - args.GlobalPosition.Y) / UIScale;
        if (MathF.Abs(offset) >= 1)
            _moved = true;

        var steps = MathF.Truncate(offset / StepPixelSize);
        DisplayValue = Math.Clamp(_dragStartValue + steps * Step, MinValue, MaxValue);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !Dragging)
            return;

        Dragging = false;
        if (!_moved)
        {
            _input.Text = DisplayValue.ToString("0.##", CultureInfo.InvariantCulture);
            _input.Visible = true;
            _input.GrabKeyboardFocus();
            return;
        }

        _value = DisplayValue;
        OnChange?.Invoke(DisplayValue);
    }

    private void CommitInput(string text)
    {
        _input.Visible = false;
        if (!float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return;

        parsed = Math.Clamp(parsed, MinValue, MaxValue);
        _value = parsed;
        DisplayValue = parsed;
        OnChange?.Invoke(parsed);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _input.Arrange(UIBox2.FromDimensions(new Vector2(0, (finalSize.Y - 24) / 2), new Vector2(MathF.Max(finalSize.X, 60), 24)));
        return finalSize;
    }
}

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

/// <summary>NumberInput из tgui-core: рамка с числом и единицами, перетаскивание и ввод по клику.</summary>
public sealed class TguiNumberInput : TguiDraggable
{
    public TguiNumberInput(float width = 90)
    {
        MinSize = new Vector2(width, 22);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = UIScale;
        var rect = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        handle.DrawRect(rect, Color.Black.WithAlpha(0.25f));
        handle.DrawRect(rect, Color.FromHex("#88bfff").WithAlpha(0.75f), false);

        // NumberInput__bar: полоска заполнения слева.
        var bar = UIBox2.FromDimensions(new Vector2(2, PixelSize.Y - 2 - (PixelSize.Y - 4) * Ratio(DisplayValue)),
            new Vector2(3 * scale, (PixelSize.Y - 4) * Ratio(DisplayValue)));
        handle.DrawRect(bar, Color.FromHex("#88bfff"));

        var font = Tgui.Font(12);
        var text = FormatValue(DisplayValue);
        var dims = handle.GetDimensions(font, text, scale);
        handle.DrawString(font, new Vector2(8 * scale, (PixelSize.Y - dims.Y) / 2), text, scale, Tgui.TextColor);
    }
}

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

/// <summary>Примитивы для рисования дуг tgui.</summary>
public static class TguiDraw
{
    /// <summary>Толстая дуга; углы в градусах по часовой стрелке от 12 часов.</summary>
    public static void Arc(DrawingHandleScreen handle, Vector2 center, float radius, float width, float fromDeg, float toDeg, Color color)
    {
        var segments = Math.Max(4, (int) MathF.Ceiling(MathF.Abs(toDeg - fromDeg) / 4));
        var verts = new List<Vector2>(segments * 6);
        var inner = radius - width / 2;
        var outer = radius + width / 2;
        for (var i = 0; i < segments; i++)
        {
            var a0 = MathF.PI / 180 * (fromDeg + (toDeg - fromDeg) * i / segments);
            var a1 = MathF.PI / 180 * (fromDeg + (toDeg - fromDeg) * (i + 1) / segments);
            var d0 = new Vector2(MathF.Sin(a0), -MathF.Cos(a0));
            var d1 = new Vector2(MathF.Sin(a1), -MathF.Cos(a1));
            verts.Add(center + d0 * inner);
            verts.Add(center + d0 * outer);
            verts.Add(center + d1 * outer);
            verts.Add(center + d0 * inner);
            verts.Add(center + d1 * outer);
            verts.Add(center + d1 * inner);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, verts.ToArray(), color);
    }

    public static void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { a, b, c, a, c, d }, color);
    }

    public static void Triangle(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { a, b, c }, color);
    }
}

/// <summary>Составные элементы tgui: LabeledControls, Collapsible, Tabs, Button.Checkbox.</summary>
public static class TguiLayout
{
    /// <summary>LabeledControls.Item: элемент по центру, подпись цвета label под ним.</summary>
    public static Control LabeledControl(string label, Control control)
    {
        control.HorizontalAlignment = Control.HAlignment.Center;
        var text = Tgui.Text(label, Tgui.Label, align: Control.HAlignment.Center);
        var box = Tgui.VBox(4, control, text);
        box.Margin = new Thickness(6, 2);
        return box;
    }

    /// <summary>Collapsible: кнопка на всю ширину со стрелкой, по клику показывает содержимое.</summary>
    public static Control Collapsible(string title, Control content, bool open = false)
    {
        var button = new TguiButton(title, Tgui.Primary) { HorizontalExpand = true };
        button.TextLabel.HorizontalAlignment = Control.HAlignment.Left;
        content.Visible = open;
        button.SetIcon(open ? "chevron-down" : "chevron-right");
        button.OnPressed += _ =>
        {
            content.Visible = !content.Visible;
            button.SetIcon(content.Visible ? "chevron-down" : "chevron-right");
        };
        return Tgui.VBox(4, button, content);
    }

    /// <summary>Button.Checkbox: прозрачная кнопка с квадратиком.</summary>
    public static TguiButton Checkbox(string text, bool @checked, Action onPressed)
    {
        var button = new TguiButton(text, @checked ? Tgui.Green : Tgui.Transparent);
        button.SetIcon(@checked ? "check-square-o" : "square-o");
        button.OnPressed += _ => onPressed();
        return button;
    }
}

/// <summary>Tabs из tgui-core: вкладки с подсветкой выбранной и полосой снизу.</summary>
public sealed class TguiTabs : BoxContainer
{
    private readonly List<(TguiTab Tab, Action OnSelect)> _tabs = new();

    public TguiTabs()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 2;
    }

    public void AddTab(string text, string? icon, bool selected, Action onSelect)
    {
        var tab = new TguiTab(text, icon) { Selected = selected };
        tab.OnPressed += _ =>
        {
            foreach (var (other, _) in _tabs)
                other.Selected = other == tab;
            onSelect();
        };
        _tabs.Add((tab, onSelect));
        AddChild(tab);
    }

    private sealed class TguiTab : BaseButton
    {
        private bool _selected;

        public TguiTab(string text, string? icon)
        {
            MouseFilter = MouseFilterMode.Stop;
            var box = Tgui.HBox(6);
            box.Margin = new Thickness(10, 4, 10, 6);
            if (icon != null)
                box.AddChild(TguiIcons.Icon(icon, 12, Tgui.TextColor));
            box.AddChild(Tgui.Text(text, Tgui.TextColor));
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
            if (_selected)
            {
                handle.DrawRect(rect, Color.White.WithAlpha(0.075f));
                handle.DrawRect(new UIBox2(0, PixelSize.Y - 2 * UIScale, PixelSize.X, PixelSize.Y), Color.FromHex("#d4dfec"));
            }
            else if (DrawMode == DrawModeEnum.Hover)
            {
                handle.DrawRect(rect, Color.White.WithAlpha(0.04f));
            }

            foreach (var child in Children)
            {
                foreach (var label in child.Children)
                {
                    if (label is Label l)
                        l.FontColorOverride = _selected || DrawMode == DrawModeEnum.Hover ? Tgui.TextColor : Color.White.WithAlpha(0.5f);
                }
            }
        }
    }
}
