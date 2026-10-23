using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>Окна ИИ: списки камер и целей, тревоги станции, оглашение законов.</summary>
public sealed class ImperialStationAiUiSystem : EntitySystem
{
    private DefaultWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialAiListEvent>(OnList);
        SubscribeNetworkEvent<ImperialAiAlertsEvent>(OnAlerts);
        SubscribeNetworkEvent<ImperialAiLawsEvent>(OnLaws);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    private void Show(DefaultWindow window)
    {
        _window?.Close();
        _window = window;
        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };
        window.OpenCentered();
    }

    private void OnList(ImperialAiListEvent ev)
    {
        var title = Loc.GetString(ev.Kind == ImperialAiListKind.Cameras ? "imperial-ai-list-cameras" : "imperial-ai-list-track");
        var window = new ImperialAiListWindow(title, ev.Entries);
        window.OnChosen += entity =>
        {
            RaiseNetworkEvent(new ImperialAiListChoiceEvent(ev.Kind, entity));
            window.Close();
        };
        Show(window);
    }

    private void OnAlerts(ImperialAiAlertsEvent ev)
    {
        var window = new ImperialAiAlertsWindow(ev.Alarms);
        window.OnJump += entity => RaiseNetworkEvent(new ImperialAiJumpEvent(entity));
        Show(window);
    }

    private void OnLaws(ImperialAiLawsEvent ev)
    {
        var window = new ImperialAiLawsWindow(ev.Laws, ev.Channels);
        window.OnState += (laws, channel) =>
        {
            RaiseNetworkEvent(new ImperialAiStateLawsEvent(laws, channel));
            window.Close();
        };
        Show(window);
    }
}

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

/// <summary>checklaws(): галочки у законов, выбор канала и «Огласить законы».</summary>
public sealed class ImperialAiLawsWindow : DefaultWindow
{
    public event Action<List<int>, string?>? OnState;

    private readonly HashSet<int> _checked;
    private string? _channel;
    private readonly BoxContainer _root = Tgui.VBox(6);
    private readonly List<string> _laws;
    private readonly List<ImperialAiChannelEntry> _channels;

    public ImperialAiLawsWindow(List<string> laws, List<ImperialAiChannelEntry> channels)
    {
        Title = Loc.GetString("imperial-ai-laws-title");
        MinSize = SetSize = new Vector2(450, 420);
        _laws = laws;
        _channels = channels;
        _checked = Enumerable.Range(0, laws.Count).ToHashSet();
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));
        Rebuild();
    }

    private void Rebuild()
    {
        _root.RemoveAllChildren();

        var lawList = Tgui.VBox(4);
        for (var i = 0; i < _laws.Count; i++)
        {
            var index = i;
            var check = TguiLayout.Checkbox(string.Empty, _checked.Contains(index), () =>
            {
                if (!_checked.Remove(index))
                    _checked.Add(index);
                Rebuild();
            });
            check.VerticalAlignment = VAlignment.Top;

            var text = Tgui.Paragraph(_laws[i]);
            text.HorizontalExpand = true;
            lawList.AddChild(Tgui.HBox(6, check, text));
        }

        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-ai-laws-title"), null, lawList));

        var channels = new GridContainer { Columns = 3, HSeparationOverride = 4, VSeparationOverride = 4 };
        channels.AddChild(Tgui.Button(Loc.GetString("imperial-ai-laws-local"), () =>
        {
            _channel = null;
            Rebuild();
        }, _channel == null ? Tgui.Green : null));

        foreach (var channel in _channels)
        {
            var id = channel.Id;
            channels.AddChild(Tgui.Button(channel.Name, () =>
            {
                _channel = id;
                Rebuild();
            }, _channel == id ? Tgui.Green : null));
        }

        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-ai-laws-channel"), null, channels));

        var state = Tgui.Button(Loc.GetString("imperial-ai-laws-state"),
            () => OnState?.Invoke(_checked.Order().ToList(), _channel), Tgui.Good, disabled: _checked.Count == 0, fluid: true);
        state.MinHeight = 28;
        _root.AddChild(state);
    }
}
