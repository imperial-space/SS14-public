using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

public sealed class CultConstructionSelectWindow : DefaultWindow
{
    public event Action<string>? OnStructureSelected;

    private static readonly (string LocKey, string ConstructionId)[] Structures =
    {
        ("cult-construct-altar",    "CultConstructionAltar"),
        ("cult-construct-forge",    "CultConstructionForge"),
        ("cult-construct-archives", "CultConstructionArchives"),
        ("cult-construct-pylon",    "CultConstructionPylon"),
        ("cult-construct-airlock",  "CultConstructionAirlock"),
        ("cult-construct-girder",   "CultConstructionGirder"),
    };

    public CultConstructionSelectWindow()
    {
        Title = Loc.GetString("cult-construction-window-title");
        MinSize = new Vector2(260, 280);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        foreach (var (locKey, constructionId) in Structures)
        {
            var capturedId = constructionId;
            var btn = new Button
            {
                Text = Loc.GetString(locKey),
                HorizontalExpand = true,
                MinSize = new Vector2(0, 36),
            };
            btn.OnPressed += _ =>
            {
                OnStructureSelected?.Invoke(capturedId);
                Close();
            };
            vbox.AddChild(btn);
        }

        scroll.AddChild(vbox);
        Contents.AddChild(scroll);
    }
}