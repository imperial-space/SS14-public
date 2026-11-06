using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>tgui_input_list (ListInputModal): список с поиском, выбор двойным кликом или «Готово».</summary>
public sealed class ImperialAiListWindow : DefaultWindow
{
    public event Action<NetEntity>? OnChosen;

    private readonly List<ImperialAiListEntry> _entries;
    private readonly BoxContainer _list = Tgui.VBox(0);
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private ImperialAiListEntry? _selected;

    public ImperialAiListWindow(string title, List<ImperialAiListEntry> entries)
    {
        Title = title;
        MinSize = SetSize = new Vector2(325, 400);
        _entries = entries;
        _selected = entries.FirstOrDefault();

        _search.PlaceHolder = Loc.GetString("imperial-ai-list-search");
        _search.ToolTip = Loc.GetString("imperial-ai-list-search-tooltip");
        _search.OnTextChanged += _ =>
        {
            _selected = Filtered().FirstOrDefault();
            Rebuild();
        };
        _search.OnTextEntered += _ => Submit();

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_list);

        var ok = Tgui.Button(Loc.GetString("imperial-ai-list-ok"), Submit, Tgui.Good);
        ok.MinWidth = 72;
        var cancel = Tgui.Button(Loc.GetString("imperial-ai-list-cancel"), Close, Tgui.Bad);
        cancel.MinWidth = 72;
        var buttons = Tgui.HBox(6, ok, cancel);
        buttons.HorizontalAlignment = HAlignment.Center;

        var body = Tgui.VBox(4, scroll, _search, buttons);
        body.VerticalExpand = true;
        var section = Tgui.MakeSection(title, null, body);
        section.VerticalExpand = true;

        var root = Tgui.VBox(0, section);
        root.Margin = new Thickness(6);
        root.VerticalExpand = true;
        Contents.AddChild(Tgui.Window(root, scrollable: false));
        Rebuild();
        _search.GrabKeyboardFocus();
    }

    private IEnumerable<ImperialAiListEntry> Filtered()
    {
        var query = _search.Text.Trim();
        return query.Length == 0
            ? _entries
            : _entries.Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                                  || e.Detail.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void Submit()
    {
        if (_selected != null)
            OnChosen?.Invoke(_selected.Entity);
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        var index = 0;
        foreach (var entry in Filtered())
        {
            var text = entry.Detail.Length > 0 ? $"{entry.Name} — {entry.Detail}" : entry.Name;
            var button = new TguiButton(text, entry == _selected ? Tgui.Green : Tgui.Transparent) { HorizontalExpand = true };
            button.TextLabel.HorizontalAlignment = HAlignment.Left;
            var current = entry;
            button.OnPressed += args =>
            {
                // onDoubleClick: повторный клик по выбранному — выбрать.
                if (_selected == current)
                {
                    Submit();
                    return;
                }

                _selected = current;
                Rebuild();
            };

            _list.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = index++ % 2 == 1 ? Color.Black.WithAlpha(0.25f) : Color.Transparent },
                Children = { button },
            });
        }

        if (index == 0)
            _list.AddChild(Tgui.Text(Loc.GetString("imperial-ai-list-empty"), Tgui.Label));
    }
}
