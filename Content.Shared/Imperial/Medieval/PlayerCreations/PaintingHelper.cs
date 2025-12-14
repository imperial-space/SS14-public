using System.Linq;

namespace Content.Shared.Imperial.Medieval.PlayerCreations;

public static class PaintingHelper
{
    public static string ColorsToString(Color[] color)
    {
        var arr = color
            .Select(c => c.ToHex())
            .ToArray();

        return string.Join("|", arr);
    }

    public static Color[] StringToColors(string str)
    {
        if (string.IsNullOrEmpty(str))
            return Array.Empty<Color>();

        return str
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Color.TryParse(s, out var v) ? v : Color.White)
            .ToArray();
    }
}
