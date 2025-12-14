using System.Numerics;
using Content.Client.Guidebook.Controls;
using Content.Client.Message;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.ControlExtensions;
using Content.Client.UserInterface.Controls;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Mobs;
using Content.Shared.Timing;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Imperial.Chemistry;

public sealed class PotionBookWindow
    : FancyWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private readonly ScrollContainer _box;
    private readonly BoxContainer _container;
    private readonly LineEdit _search;

    public PotionBookWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("imperial-medieval-recipebook");
        MinSize = new Vector2(100, 200);
        Resizable = true;
        SetSize = new Vector2(900, 700);
        _container = new BoxContainer()
        {
            Orientation = LayoutOrientation.Vertical
        };
        _search = new LineEdit()
        {
            PlaceHolder = Loc.GetString("guidebook-filter-placeholder-text"),
            HorizontalExpand = true
        };
        _box = new ScrollContainer()
        {
            HScrollEnabled = false,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = {
                new Control()
                {
                    Children = {
                        _container
                    }
                }
            }
        };
        AddChild(new BoxContainer()
        {
            Orientation = LayoutOrientation.Vertical,
            Children = {
                _search,
                _box
            },
            Margin = new Thickness(5, 28, 5, 5)
        });
        _search.OnTextChanged += (_) =>
        {
            foreach (var entry in _container.Children)
            {
                if (SearchForText(entry, _search.Text))
                    entry.Visible = true;
                else
                    entry.Visible = false;
            }
        };
    }
    private bool SearchForText(Control searchin, string text)
    {
        if (searchin is Label label)
        {
            if (label.Text != null)
                if (label.Text.ToLower().Contains(text.ToLower()))
                    return true;
        }
        if (searchin is RichTextLabel richlabel)
        {
            if (richlabel.Text != null)
                if (richlabel.Text.ToLower().Contains(text.ToLower()))
                    return true;
        }
        foreach (var child in searchin.Children)
        {
            if (SearchForText(child, text))
                return true;
        }
        return false;
    }

    public void UpdateState(PotionBookUserInterfaceState state)
    {
        _container.DisposeAllChildren();
        foreach (var id in state.Ids)
        {
            var proto = _proto.Index<ReagentPrototype>(id);
            _container.AddChild(new GuideReagentEmbed(proto, true) { HorizontalExpand = true });
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }
}
