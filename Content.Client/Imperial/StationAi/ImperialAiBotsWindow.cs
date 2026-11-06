using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>RemoteRobotControl: боты станции, слежение и вызов к взгляду ИИ.</summary>
public sealed class ImperialAiBotsWindow : DefaultWindow
{
    public event Action<NetEntity, ImperialAiBotCommand>? OnCommand;

    public ImperialAiBotsWindow(List<ImperialAiBotEntry> bots)
    {
        Title = Loc.GetString("imperial-ai-bots-title");
        MinSize = SetSize = new Vector2(420, 460);

        var list = Tgui.VBox(2);
        if (bots.Count == 0)
            list.AddChild(Tgui.NoticeBox(Loc.GetString("imperial-ai-bots-none")));

        var index = 0;
        foreach (var bot in bots)
        {
            var entity = bot.Entity;
            var name = Tgui.Text(bot.Name);
            var where = Tgui.Text(bot.Location, Tgui.Label, 10);
            var info = Tgui.VBox(0, name, where);
            info.HorizontalExpand = true;

            var track = Tgui.Button(Loc.GetString("imperial-ai-borgs-track"), () => OnCommand?.Invoke(entity, ImperialAiBotCommand.Track));
            var summon = Tgui.Button(Loc.GetString(bot.Summoned ? "imperial-ai-bots-summoned" : "imperial-ai-bots-summon"),
                () => OnCommand?.Invoke(entity, ImperialAiBotCommand.Summon), Tgui.Green);
            track.VerticalAlignment = summon.VerticalAlignment = VAlignment.Center;

            var row = Tgui.HBox(6, info, track, summon);
            row.Margin = new Thickness(4, 2);
            list.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = index++ % 2 == 1 ? Color.Black.WithAlpha(0.25f) : Color.Transparent },
                Children = { row },
            });
        }

        var root = Tgui.VBox(0, Tgui.MakeSection(Loc.GetString("imperial-ai-bots-title"), null, list));
        root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(root));
    }
}
