using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.Imperial.Medieval.Lycantropy.UI;

public sealed partial class LycantropyLayout : LayoutContainer
{

    public LycantropyLayout()
    {
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        foreach (var child in Children)
        {
            if (child is not LycantropyAbilityEntry button)
                continue;

            DrawLines(handle, button);
        }
    }

    private void DrawLines(DrawingHandleScreen handle, LycantropyAbilityEntry entry)
    {
        var proto = entry.Proto;
        var pos = entry.PixelPosition + new Vector2(entry.PixelWidth / 2, entry.PixelHeight / 2);
        foreach (var child in Children)
        {
            if (child is not LycantropyAbilityEntry other)
                continue;

            if (!proto.Required.Contains(other.Proto.ID))
                continue;

            var targetPos = other.PixelPosition + new Vector2(other.PixelWidth / 2, other.PixelHeight / 2);
            handle.DrawLine(pos, targetPos, Color.WhiteSmoke);
        }
    }

}
