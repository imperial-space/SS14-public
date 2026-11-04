using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.UI;

/// <summary>Цвета tgui по имени (ranges / color у ProgressBar, Knob, RoundGauge).</summary>
public static class TguiColors
{
    public static Color Get(string name)
    {
        return name switch
        {
            "good" => Tgui.Good,
            "average" => Tgui.Average,
            "bad" => Tgui.Bad,
            "teal" => Color.FromHex("#00b5ad"),
            "grey" => Color.FromHex("#646464"),
            "black" => Color.FromHex("#1a1a1a"),
            "yellow" => Tgui.Yellow,
            "orange" => Color.FromHex("#f2711c"),
            "blue" => Tgui.Blue,
            "label" => Tgui.Label,
            _ => Tgui.PrimaryLine,
        };
    }
}
