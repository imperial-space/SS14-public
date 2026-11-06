using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Силуэт борта по его плиткам, цветом стороны.</summary>
public sealed class CadiaShipSilhouette(CadiaAirContact contact, Color color) : Control
{
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (contact.SilhouetteWidth == 0)
            return;
        var cell = MathF.Min(PixelSize.X / contact.SilhouetteWidth, PixelSize.Y / contact.SilhouetteHeight);
        var origin = (PixelSize - new Vector2(contact.SilhouetteWidth, contact.SilhouetteHeight) * cell) / 2f;
        for (var y = 0; y < contact.SilhouetteHeight; y++)
        {
            for (var x = 0; x < contact.SilhouetteWidth; x++)
            {
                if (contact.Silhouette[y * contact.SilhouetteWidth + x] == 0)
                    continue;
                // Сетка снизу вверх, экран сверху вниз.
                var pos = origin + new Vector2(x * cell, (contact.SilhouetteHeight - 1 - y) * cell);
                handle.DrawRect(UIBox2.FromDimensions(pos, new Vector2(MathF.Max(1, cell - 0.5f))), color);
            }
        }
    }
}
