using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>Подключённые киборги: целостность, заряд, где находятся; слежение и вход в оболочку.</summary>
public sealed class ImperialAiBorgsWindow : DefaultWindow
{
    public event Action<NetEntity, ImperialAiBorgCommand>? OnCommand;

    public ImperialAiBorgsWindow(List<ImperialAiBorgEntry> borgs)
    {
        Title = Loc.GetString("imperial-ai-borgs-title");
        MinSize = SetSize = new Vector2(420, 480);

        var root = Tgui.VBox(6);
        root.Margin = new Thickness(6);
        if (borgs.Count == 0)
            root.AddChild(Tgui.NoticeBox(Loc.GetString("imperial-ai-borgs-none")));

        foreach (var borg in borgs)
        {
            var entity = borg.Entity;
            var buttons = Tgui.HBox(4,
                Tgui.Button(Loc.GetString("imperial-ai-borgs-track"), () => OnCommand?.Invoke(entity, ImperialAiBorgCommand.Track)));
            if (borg.Shell)
            {
                buttons.AddChild(Tgui.Button(Loc.GetString(borg.Deployed ? "imperial-ai-borgs-deployed" : "imperial-ai-borgs-deploy"),
                    () => OnCommand?.Invoke(entity, ImperialAiBorgCommand.Deploy), Tgui.Green, disabled: borg.Deployed));
            }

            var info = Tgui.LabeledList(
                (Loc.GetString("imperial-ai-borgs-location"), Tgui.Text(borg.Location)),
                (Loc.GetString("imperial-ai-borgs-health"), Tgui.ProgressBar(borg.Health / 100f, Tgui.RangeColor(borg.Health / 100f), $"{borg.Health}%")),
                (Loc.GetString("imperial-ai-borgs-charge"), Tgui.ProgressBar(borg.Charge / 100f, Tgui.RangeColor(borg.Charge / 100f), $"{borg.Charge}%")));

            var title = borg.Shell ? $"{borg.Name} — {Loc.GetString("imperial-ai-borgs-shell")}" : borg.Name;
            root.AddChild(Tgui.MakeSection(title, buttons, info));
        }

        Contents.AddChild(Tgui.Window(root));
    }
}
