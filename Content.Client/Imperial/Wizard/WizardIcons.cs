using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Wizard;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Imperial.Wizard;

/// <summary>Иконки Font Awesome для книги (имя → код).</summary>
public static class WizardIcons
{
    private static readonly Dictionary<string, int> Codes = new()
    {
        ["plus"] = 0xf067, ["hat-wizard"] = 0xf6e8, ["meteor"] = 0xf753, ["tshirt"] = 0xf553,
        ["arrow-left"] = 0xf060, ["arrow-right"] = 0xf061, ["arrow-rotate-left"] = 0xf0e2, ["pen"] = 0xf304,
        ["clipboard"] = 0xf328, ["fire"] = 0xf06d, ["shield-alt"] = 0xf3ed, ["globe-americas"] = 0xf57d,
        ["users"] = 0xf0c0, ["crown"] = 0xf521, ["magic"] = 0xf0d0, ["thumbs-up"] = 0xf164, ["dice"] = 0xf522,
        ["cog"] = 0xf013, ["lock"] = 0xf023, ["dollar-sign"] = 0x24, ["dice-three"] = 0xf527, ["hammer"] = 0xf6e3,
        ["skull"] = 0xf54c, ["pastafarianism"] = 0xf67b,
    };

    public static Label Icon(string name, int size = 12, Color? color = null)
    {
        return new Label
        {
            Text = Codes.TryGetValue(name, out var code) ? char.ConvertFromUtf32(code) : "?",
            FontOverride = TguiIcons.Font(size),
            FontColorOverride = color ?? Tgui.TextColor,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    /// <summary>Кнопка с иконкой слева (Button icon=...).</summary>
    public static TguiButton Button(string text, string icon, Action? onPressed = null, Color? color = null, bool disabled = false,
        bool fluid = false, string? tooltip = null, TguiTheme? theme = null)
    {
        var button = Tgui.Button(text, onPressed, color, disabled, fluid, tooltip: tooltip, theme: theme);
        var glyph = Icon(icon);
        glyph.Margin = new Thickness(4, 0, 0, 0);
        var content = (BoxContainer) ((PanelContainer) button.Children.First()).Children.First();
        content.AddChild(glyph);
        glyph.SetPositionFirst();
        if (string.IsNullOrEmpty(text))
            button.TextLabel.Visible = false;
        return button;
    }

    /// <summary>Button.Confirm: первый клик — «подтвердить?», второй — действие.</summary>
    public static TguiButton Confirm(string text, string icon, string confirmText, Action onConfirm, Color? color = null,
        TguiTheme? theme = null, int height = 22)
    {
        var armed = false;
        TguiButton? button = null;
        button = Button(text, icon, () =>
        {
            if (!armed)
            {
                armed = true;
                button!.TextLabel.Text = confirmText;
                button.Color = Tgui.Green;
                return;
            }

            armed = false;
            button!.TextLabel.Text = text;
            button.Color = color ?? (theme ?? TguiTheme.Default).Primary;
            onConfirm();
        }, color, fluid: true, theme: theme);
        button.MinHeight = height;
        return button;
    }
}
