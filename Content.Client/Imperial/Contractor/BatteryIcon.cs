using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Contractor;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Contractor;

/// <summary>Значок батареи NTOS: корпус с полной заливкой и контакт справа.</summary>
public sealed class BatteryIcon : Control
{
    private static readonly Color Body = Color.FromHex("#9fd09f");

    public BatteryIcon()
    {
        MinSize = new Vector2(22, 12);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var scale = UIScale;
        var tip = 2 * scale;
        var border = 1 * scale;
        var body = new UIBox2(0, 0, size.X - tip, size.Y);

        handle.DrawRect(body, Body, filled: false);
        handle.DrawRect(new UIBox2(body.Left + border * 2, body.Top + border * 2, body.Right - border * 2, body.Bottom - border * 2), Body);
        handle.DrawRect(new UIBox2(size.X - tip, size.Y * 0.3f, size.X, size.Y * 0.7f), Body);
    }
}
