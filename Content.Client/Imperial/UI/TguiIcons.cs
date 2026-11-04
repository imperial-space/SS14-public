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
/// Иконки tgui (Font Awesome Free 6, SIL OFL 1.1 — Resources/Fonts/Imperial/FontAwesome).
/// Имена — как в tgui-интерфейсах SS13, включая старые имена FA4 (snowflake-o, sun-o и т.п.).
/// </summary>
public static class TguiIcons
{
    private const string SolidPath = "/Fonts/Imperial/FontAwesome/fa-solid-900.ttf";
    private const string RegularPath = "/Fonts/Imperial/FontAwesome/fa-regular-400.ttf";

    private static readonly Dictionary<string, (int Code, bool Regular)> Glyphs = new()
    {
        ["power-off"] = (0xf011, false),
        ["times"] = (0xf00d, false),
        ["question-circle"] = (0xf059, false),
        ["fire"] = (0xf06d, false),
        ["snowflake-o"] = (0xf2dc, true),
        ["snowflake"] = (0xf2dc, false),
        ["magnet"] = (0xf076, false),
        ["sun-o"] = (0xf185, true),
        ["fast-forward"] = (0xf050, false),
        ["fast-backward"] = (0xf049, false),
        ["undo"] = (0xf0e2, false),
        ["angle-up"] = (0xf106, false),
        ["angle-down"] = (0xf107, false),
        ["angle-double-up"] = (0xf102, false),
        ["angle-double-down"] = (0xf103, false),
        ["minus"] = (0xf068, false),
        ["window-minimize"] = (0xf2d1, false),
        ["arrow-up"] = (0xf062, false),
        ["arrow-down"] = (0xf063, false),
        ["thermometer-full"] = (0xf2c7, false),
        ["square-o"] = (0xf0c8, true),
        ["check-square-o"] = (0xf14a, true),
        ["chevron-up"] = (0xf077, false),
        ["chevron-down"] = (0xf078, false),
        ["chevron-right"] = (0xf054, false),
        ["wrench"] = (0xf0ad, false),
        ["atom"] = (0xf5d2, false),
        ["cubes"] = (0xf1b3, false),
        ["exclamation-triangle"] = (0xf071, false),
    };

    private static readonly Dictionary<(int, bool), Font> Cache = new();

    public static Font Font(int size, bool regular = false)
    {
        if (Cache.TryGetValue((size, regular), out var font))
            return font;

        var cache = IoCManager.Resolve<IResourceCache>();
        font = new VectorFont(cache.GetResource<FontResource>(regular ? RegularPath : SolidPath), size);
        Cache[(size, regular)] = font;
        return font;
    }

    public static bool TryGet(string name, out string glyph, out bool regular)
    {
        if (Glyphs.TryGetValue(name, out var entry))
        {
            glyph = char.ConvertFromUtf32(entry.Code);
            regular = entry.Regular;
            return true;
        }

        glyph = string.Empty;
        regular = false;
        return false;
    }

    /// <summary>Icon из tgui: глиф Font Awesome нужного размера и цвета.</summary>
    public static Label Icon(string name, int size = 12, Color? color = null)
    {
        TryGet(name, out var glyph, out var regular);
        return new Label
        {
            Text = glyph,
            FontOverride = Font(size, regular),
            FontColorOverride = color ?? Tgui.TextColor,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
        };
    }

    /// <summary>Icon.Stack: несколько глифов друг на друге.</summary>
    public static Control Stack(int size, Color color, params string[] names)
    {
        var layout = new LayoutContainer { MinSize = new Vector2(size * 1.25f, size * 1.25f) };
        foreach (var name in names)
        {
            var icon = Icon(name, size, color);
            layout.AddChild(icon);
            LayoutContainer.SetAnchorAndMarginPreset(icon, LayoutContainer.LayoutPreset.Wide);
        }

        return layout;
    }

    public static void DrawGlyph(DrawingHandleScreen handle, string name, int size, Vector2 center, Color color, float scale)
    {
        if (!TryGet(name, out var glyph, out var regular))
            return;

        var font = Font(size, regular);
        var dims = handle.GetDimensions(font, glyph, scale);
        handle.DrawString(font, center - dims / 2, glyph, scale, color);
    }
}
