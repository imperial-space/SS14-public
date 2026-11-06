using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Contractor;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Contractor;

/// <summary>Значок сети NTOS: четыре столбика нарастающей высоты.</summary>
public sealed class SignalIcon : Control
{
    private static readonly Color Bar = Color.FromHex("#9fd09f");

    public SignalIcon()
    {
        MinSize = new Vector2(16, 14);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var scale = UIScale;
        var barWidth = 3 * scale;
        var gap = 1 * scale;
        for (var i = 0; i < 4; i++)
        {
            var height = size.Y * (i + 1) / 4f;
            var x = i * (barWidth + gap);
            handle.DrawRect(new UIBox2(x, size.Y - height, x + barWidth, size.Y), Bar);
        }
    }
}
