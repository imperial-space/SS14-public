using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.StationAi;

/// <summary>AiStatusDisplayPicker: картинки статус-дисплеев с подписями, текущая подсвечена.</summary>
public sealed class ImperialAiStatusDisplayWindow : DefaultWindow
{
    public event Action<string?>? OnChosen;

    private static readonly ResPath Rsi = new("/Textures/Imperial/StationAi/status_display.rsi");

    public ImperialAiStatusDisplayWindow(SpriteSystem sprites, string? current)
    {
        Title = Loc.GetString("imperial-ai-display-title");
        MinSize = SetSize = new Vector2(480, 520);

        var grid = new GridContainer { Columns = 4, HSeparationOverride = 6, VSeparationOverride = 6 };
        var none = Tgui.Button(Loc.GetString("imperial-ai-display-none"), () => Choose(null), current == null ? Tgui.Green : null);
        none.MinSize = new Vector2(100, 92);
        grid.AddChild(none);

        foreach (var (name, state) in ImperialAiVox.DisplayOptions.DistinctBy(o => o.Name))
        {
            var option = state;
            var button = new TguiButton(string.Empty, option == current ? Tgui.Green : Tgui.Transparent) { MinSize = new Vector2(100, 92) };
            button.TextLabel.Visible = false;
            var icon = new TextureRect
            {
                Texture = sprites.Frame0(new SpriteSpecifier.Rsi(Rsi, state)),
                TextureScale = new Vector2(2, 2),
                Stretch = TextureRect.StretchMode.KeepCentered,
                MinSize = new Vector2(64, 64),
            };
            var label = Tgui.Text(name, size: 10, align: Control.HAlignment.Center);
            var box = Tgui.VBox(2, icon, label);
            box.Margin = new Thickness(2);
            button.AddChild(box);
            button.OnPressed += _ => Choose(option);
            grid.AddChild(button);
        }

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(grid);
        var section = Tgui.MakeSection(Loc.GetString("imperial-ai-display-title"), null, scroll);
        section.VerticalExpand = true;
        var root = Tgui.VBox(0, section);
        root.Margin = new Thickness(6);
        root.VerticalExpand = true;
        Contents.AddChild(Tgui.Window(root, scrollable: false));
    }

    private void Choose(string? state)
    {
        OnChosen?.Invoke(state);
        Close();
    }
}
