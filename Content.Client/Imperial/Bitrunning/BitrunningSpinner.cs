using System.Numerics;
using Content.Client.Imperial.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Bitrunning;

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
