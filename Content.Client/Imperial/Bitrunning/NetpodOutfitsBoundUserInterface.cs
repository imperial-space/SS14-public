using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

[UsedImplicitly]
public sealed class NetpodOutfitsBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private NetpodOutfitsWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<NetpodOutfitsWindow>();
        _window.OnSelect += id => SendMessage(new NetpodSelectOutfitMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is NetpodOutfitState outfits)
            _window?.Update(outfits);
    }
}

/// <summary>Окно нетпода 1 в 1 с tgui NetpodOutfits (400×300): коллекции слева, образы справа, поиск.</summary>
public sealed class NetpodOutfitsWindow : DefaultWindow
{
    public event Action<string>? OnSelect;

    private readonly BoxContainer _root = Tgui.VBox(6);
    private readonly LineEdit _search = new() { MinWidth = 120 };
    private readonly BoxContainer _list = Tgui.VBox(0);
    private NetpodOutfitState? _state;

    public NetpodOutfitsWindow()
    {
        Title = Loc.GetString("bitrunning-netpod-ui-title");
        MinSize = SetSize = new Vector2(400, 300);
        _root.Margin = new Thickness(6);
        _root.VerticalExpand = true;
        _search.PlaceHolder = Loc.GetString("bitrunning-netpod-ui-search");
        _search.OnTextChanged += _ => RebuildList();
        Contents.AddChild(Tgui.Window(_root, scrollable: false));
    }

    public void Update(NetpodOutfitState state)
    {
        _state = state;
        _search.Orphan();
        _list.Orphan();
        _root.RemoveAllChildren();

        // Tabs vertical: единственная коллекция «Jobs».
        var tab = new TguiButton(Loc.GetString("bitrunning-netpod-ui-jobs"), Color.White.WithAlpha(0.075f)) { HorizontalExpand = true };
        tab.TextLabel.HorizontalAlignment = HAlignment.Left;
        var tabs = Tgui.VBox(2, tab);
        tabs.HorizontalExpand = true;
        tabs.SizeFlagsStretchRatio = 1;

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(_list);
        scroll.SizeFlagsStretchRatio = 5;

        var body = Tgui.HBox(0, tabs, BitrunningTgui.VDivider(), scroll);
        body.VerticalExpand = true;

        var section = Tgui.MakeSection(Loc.GetString("bitrunning-netpod-ui-select"), _search, body);
        section.VerticalExpand = true;
        _root.AddChild(section);

        var selected = state.Jobs.Find(j => j.Id == state.Selected)?.Name ?? Loc.GetString("bitrunning-netpod-ui-none");
        var notice = Tgui.NoticeBox(selected, Tgui.Blue);
        if (notice.GetChild(0) is Label label)
            label.HorizontalAlignment = HAlignment.Right;
        _root.AddChild(notice);

        RebuildList();
    }

    private void RebuildList()
    {
        if (_state is not { } state)
            return;

        _list.RemoveAllChildren();
        var search = _search.Text.Trim();
        var index = 0;
        foreach (var outfit in state.Jobs
                     .Where(j => search.Length == 0 || j.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(j => j.Name))
        {
            var id = outfit.Id;
            var button = new TguiButton(outfit.Name, state.Selected == id ? Tgui.Green : Tgui.Transparent) { HorizontalAlignment = HAlignment.Left };
            button.OnPressed += _ => OnSelect?.Invoke(id);

            // candystripe: каждая вторая строка затемнена.
            var row = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = index++ % 2 == 1 ? Color.Black.WithAlpha(0.25f) : Color.Transparent },
                Children = { button },
            };
            _list.AddChild(row);
        }
    }
}
