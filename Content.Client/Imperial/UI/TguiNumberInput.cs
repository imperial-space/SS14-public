using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.UI;

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
