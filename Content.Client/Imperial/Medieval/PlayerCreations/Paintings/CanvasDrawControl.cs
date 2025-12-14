using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.PlayerCreations.Paintings;

public sealed class CanvasDrawControl : Control
{
    public Color[] Pixels = new Color[30 * 30];
    public Color DrawColor = Color.Black;
    private int _textureWidth = 30;
    private int _textureHeight = 30;

    private bool _drawing;
    private bool _erasing;

    public ColorSelectorSliders? SelectorSliders;

    public Action? OnLeftDown;
    public Action? OnRightDown;
    public Action? OnMiddleDown;

    public Action? OnClickUp;


    public CanvasDrawControl()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    public void SetTextureSize(Vector2i size)
    {
        _textureWidth = size.X;
        _textureHeight = size.Y;

        Pixels = new Color[size.X * size.Y];
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var pixelSize = PixelWidth / _textureWidth;

        for (var y = 0; y < _textureHeight; y++)
        {
            for (var x = 0; x < _textureWidth; x++)
            {
                var color = Pixels[y * _textureWidth + x];
                var rect = UIBox2.FromDimensions(
                    new(x * pixelSize, y * pixelSize),
                    new(pixelSize, pixelSize)
                );
                handle.DrawRect(rect, color == new Color(0,0,0,0) ? Color.White : color);
            }
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _drawing = true;
            OnLeftDown?.Invoke();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            _drawing = true;
            _erasing = true;
            OnRightDown?.Invoke();
        }
        else if (args.Function == "MouseMiddle")
        {
            var cursor = GetCursorPos();
            if (SelectorSliders != null)
            {
                SelectorSliders.Color = GetPixel(cursor);
                DrawColor = SelectorSliders.Color;
            }
            OnMiddleDown?.Invoke();

        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        _drawing = false;
        _erasing = false;

        OnClickUp?.Invoke();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_drawing)
            return;

        var cursor = GetCursorPos();

        SetPixel(cursor, _erasing ? Color.White : DrawColor);
    }

    private Vector2i GetCursorPos()
    {
        var controlCursor = (UserInterfaceManager.MousePositionScaled.Position * UIScale) - GlobalPixelPosition;

        var pixelCursor = GetPixelByControlPos(controlCursor);

        return pixelCursor;
    }

    private void SetPixel(Vector2i pos, Color color)
    {
        if (pos.X >= _textureWidth || pos.X < 0 || pos.Y >= _textureHeight || pos.Y < 0)
            return;

        var index = pos.Y * _textureWidth + pos.X;

        Pixels[index] = color;
    }

    private Color GetPixel(Vector2i pos)
    {
        if (pos.X >= _textureWidth || pos.X < 0 || pos.Y >= _textureHeight || pos.Y < 0)
            return Color.White;

        var index = pos.Y * _textureWidth + pos.X;

        return Pixels[index];
    }

    private Vector2i GetPixelByControlPos(Vector2 pos)
    {
        var pixelSize = PixelWidth / _textureWidth;

        var output = new Vector2i((int)(pos.X / pixelSize), (int)(pos.Y / pixelSize));

        return output;
    }
}
