using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.ModularShield;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.ModularShield;

internal static class ShieldUi
{
    /// <summary>ProgressBar с диапазонами good ≥ 75%, average 25–75%, bad &lt; 25% и подписью поверх.</summary>
    public static (Control Root, ProgressBar Bar, Label Text) Bar()
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            MinHeight = 22,
            HorizontalExpand = true,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1b1b1f") },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#5baa27") },
        };
        var text = new Label { HorizontalAlignment = Control.HAlignment.Center, VerticalAlignment = Control.VAlignment.Center };
        var root = new Control { HorizontalExpand = true, Margin = new Thickness(0, 2) };
        root.AddChild(bar);
        root.AddChild(text);
        return (root, bar, text);
    }

    public static void Set(ProgressBar bar, Label label, float value, float max, string text)
    {
        var fraction = max <= 0 ? 0 : Math.Clamp(value / max, 0, 1);
        bar.Value = fraction;
        var color = fraction >= 0.75f ? "#5baa27" : fraction >= 0.25f ? "#d09000" : "#bd2020";
        bar.ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(color) };
        label.Text = text;
    }

    public static string Num(float value) => MathF.Round(value, 2).ToString();
}
