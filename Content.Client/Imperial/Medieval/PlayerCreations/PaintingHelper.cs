using System.Linq;
using Robust.Client.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Imperial.Medieval.PlayerCreations;

public sealed class PaintingHelper
{
    public static Rgba32[] ColorToRgba32(Color[] colors)
    {
        return colors
            .Select(c => c.A == 0 ? Color.White : c)
            .Select(c => new Rgba32(c.R, c.G, c.B))
            .ToArray();
    }

    public static Texture GetTextureFromColorArray(IClyde clyde, Color[] colors, int width = 30, int height = 30)
    {
        var tex = clyde.CreateBlankTexture<Rgba32>(new(width, height));
        var rgbaArray = ColorToRgba32(colors);
        tex.SetSubImage((0, 0) ,(width, height), new ReadOnlySpan<Rgba32>(rgbaArray));

        return tex;
    }
}
