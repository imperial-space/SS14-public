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
