using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>StationAlertConsole с cameraView: категории тревог, у каждой точки — переход к источнику.</summary>
public sealed class ImperialAiAlertsWindow : DefaultWindow
{
    public event Action<NetEntity>? OnJump;

    private static readonly (ImperialAiAlarmKind Kind, string Name)[] Categories =
    {
        (ImperialAiAlarmKind.Fire, "imperial-ai-alerts-fire"),
        (ImperialAiAlarmKind.Atmosphere, "imperial-ai-alerts-atmosphere"),
        (ImperialAiAlarmKind.Power, "imperial-ai-alerts-power"),
        (ImperialAiAlarmKind.Camera, "imperial-ai-alerts-camera"),
    };

    public ImperialAiAlertsWindow(List<ImperialAiAlarmEntry> alarms)
    {
        Title = Loc.GetString("imperial-ai-alerts-title");
        MinSize = SetSize = new Vector2(390, 587);

        var root = Tgui.VBox(6);
        root.Margin = new Thickness(6);
        foreach (var (kind, name) in Categories)
        {
            var list = Tgui.VBox(2);
            var groups = alarms.Where(a => a.Kind == kind).GroupBy(a => a.Location).OrderBy(g => g.Key).ToList();
            if (groups.Count == 0)
                list.AddChild(Tgui.Text(Loc.GetString("imperial-ai-alerts-none"), Tgui.Good));

            foreach (var group in groups)
            {
                var count = group.Count();
                var label = count > 1
                    ? Loc.GetString("imperial-ai-alerts-sources", ("name", group.Key), ("count", count))
                    : group.Key;
                var text = Tgui.Text("• " + label, Tgui.Average);
                text.HorizontalExpand = true;
                text.VerticalAlignment = VAlignment.Center;

                var source = group.First().Source;
                var jump = Tgui.Button(Loc.GetString("imperial-ai-alerts-jump"), () => OnJump?.Invoke(source));
                jump.MinWidth = 100;
                var row = Tgui.HBox(6, text, jump);
                row.MinHeight = 30;
                list.AddChild(row);
            }

            root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-ai-alerts-section", ("category", Loc.GetString(name))), null, list));
        }

        Contents.AddChild(Tgui.Window(root));
    }
}
