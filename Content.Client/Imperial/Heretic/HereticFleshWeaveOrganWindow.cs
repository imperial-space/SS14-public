using System.Numerics;
using Content.Shared.Imperial.Heretic;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticFleshWeaveOrganWindow : DefaultWindow
{
    public event Action<NetEntity, NetEntity>? OnOrganSelected;

    private readonly BoxContainer _list;

    private static readonly Color BrightGreen = Color.FromHex("#55cc55");
    private static readonly Color DimGreen    = Color.FromHex("#88aa88");

    public HereticFleshWeaveOrganWindow()
    {
        Title   = Loc.GetString("heretic-flesh-weave-organ-window-title");
        MinSize = new Vector2(280, 180);

        var root = new BoxContainer
        {
            Orientation        = BoxContainer.LayoutOrientation.Vertical,
            Margin             = new Thickness(8),
            SeparationOverride = 4,
        };

        root.AddChild(new Label
        {
            Text              = Loc.GetString("heretic-flesh-weave-organ-choose-label"),
            FontColorOverride = DimGreen,
            Margin            = new Thickness(0, 0, 0, 4),
        });

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        _list = new BoxContainer
        {
            Orientation        = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        Contents.AddChild(root);
    }

    public void Populate(HereticFleshWeaveOrganBuiState state)
    {
        _list.RemoveAllChildren();

        foreach (var organ in state.Organs)
        {
            var capturedOrgan  = organ.Organ;
            var capturedTarget = state.Target;

            var btn = new Button { HorizontalExpand = true };
            btn.Label.Text              = organ.Name;
            btn.Label.FontColorOverride = BrightGreen;
            btn.OnPressed += _ =>
            {
                OnOrganSelected?.Invoke(capturedOrgan, capturedTarget);
                Close();
            };
            _list.AddChild(btn);
        }
    }
}
