using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.UI;

/// <summary>Тема tgui: цвета фона, секций, primary, подписей и плашек (tgui-core/styles/themes).</summary>
public sealed class TguiTheme
{
    public Color BaseStart = Color.FromHex("#2a2a2a");
    public Color BaseEnd = Color.FromHex("#202020");
    public Color Section = Color.FromHex("#191919");
    public Color Primary = Color.FromHex("#40628a");
    public Color SectionLine = Color.FromHex("#4972a1");
    public Color Label = Color.FromHex("#7e90a7");
    public Color Notice = Color.FromHex("#bb9b68");
    public Color NoticeText = Color.Black;
    public Color Selected = Color.FromHex("#1fa952");
    public Color Danger = Color.FromHex("#d92626");
    public Color Header = Color.FromHex("#1b1b1b");

    /// <summary>Тема по умолчанию.</summary>
    public static readonly TguiTheme Default = new();

    /// <summary>
    /// theme-syndicate: --color-base hsl(0, 96%, 17%) с градиентом 6, --color-primary hsl(120, 34%, 35%),
    /// плашки hsl(0, 98%, 28%), выбранные кнопки hsl(0, 90%, 37.5%).
    /// </summary>
    public static readonly TguiTheme Syndicate = new()
    {
        BaseStart = Color.FromHex("#6f0404"),
        BaseEnd = Color.FromHex("#3b0202"),
        Section = Color.FromHex("#360101"),
        Primary = Color.FromHex("#3b773b"),
        SectionLine = Color.FromHex("#3b773b"),
        Label = Color.FromHex("#80aa80"),
        Notice = Color.FromHex("#750101"),
        NoticeText = Color.White,
        Selected = Color.FromHex("#b50909"),
        Danger = Color.FromHex("#999900"),
        Header = Color.FromHex("#260000"),
    };
}
