using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.StationAi;

/// <summary>Меню модулей сбойного ИИ: открывается и обновляется событием с сервера.</summary>
public sealed class ImperialMalfPickerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private ImperialMalfPickerWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialMalfPickerEvent>(OnPicker);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    private void OnPicker(ImperialMalfPickerEvent ev)
    {
        if (_window == null)
        {
            if (!ev.Open)
                return;

            _window = new ImperialMalfPickerWindow(_proto, _sprite);
            _window.OnBuy += id => RaiseNetworkEvent(new ImperialMalfBuyEvent(id));
            _window.OnClose += () => _window = null;
            _window.OpenCentered();
        }

        _window.Update(ev);
    }
}

/// <summary>MalfunctionModulePicker (GenericUplink, тема malfunction): категории, поиск, модули с ценой в ПВ.</summary>
public sealed class ImperialMalfPickerWindow : DefaultWindow
{
    public event Action<string>? OnBuy;

    /// <summary>theme-malfunction: тёмно-синий фон и красные акценты.</summary>
    private static readonly TguiTheme Theme = new()
    {
        BaseStart = Color.FromHex("#1f3446"),
        BaseEnd = Color.FromHex("#14222e"),
        Section = Color.FromHex("#0f1b25"),
        Primary = Color.FromHex("#910101"),
        SectionLine = Color.FromHex("#910101"),
        Label = Color.FromHex("#8ea8c0"),
        Notice = Color.FromHex("#910101"),
        NoticeText = Color.White,
        Selected = Color.FromHex("#b20000"),
        Danger = Color.FromHex("#d92626"),
        Header = Color.FromHex("#0b141b"),
    };

    private readonly IPrototypeManager _proto;
    private readonly SpriteSystem _sprite;
    private readonly BoxContainer _root = Tgui.HBox(6);
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly BoxContainer _items = Tgui.VBox(6);
    private ImperialMalfPickerEvent? _state;
    private ImperialMalfModuleCategory _category = ImperialMalfModuleCategory.Destructive;
    private bool _compact;

    public ImperialMalfPickerWindow(IPrototypeManager proto, SpriteSystem sprite)
    {
        _proto = proto;
        _sprite = sprite;
        Title = Loc.GetString("imperial-malf-picker-title");
        MinSize = SetSize = new Vector2(620, 525);
        _root.Margin = new Thickness(6);
        _root.VerticalExpand = true;
        _search.PlaceHolder = Loc.GetString("imperial-malf-picker-search");
        _search.OnTextChanged += _ => RebuildItems();
        Contents.AddChild(Tgui.Window(_root, Theme, scrollable: false));
    }

    public void Update(ImperialMalfPickerEvent state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state is not { } state)
            return;

        _search.Orphan();
        _items.Orphan();
        _root.RemoveAllChildren();

        // Левая колонка: валюта, компактный режим, поиск, вкладки категорий.
        var currency = Tgui.Button(Loc.GetString("imperial-malf-picker-points", ("points", state.ProcessingTime)),
            color: Theme.Primary, fluid: true, bold: true, theme: Theme);
        currency.MinHeight = 32;
        var compact = Tgui.Button(_compact ? "▢" : "▭", () =>
        {
            _compact = !_compact;
            Rebuild();
        }, Theme.Primary, tooltip: Loc.GetString(_compact ? "imperial-malf-picker-detailed" : "imperial-malf-picker-compact"));
        compact.MinHeight = 32;

        var tabs = Tgui.VBox(2);
        foreach (var category in Enum.GetValues<ImperialMalfModuleCategory>())
        {
            var current = category;
            var tab = Tgui.Button(Loc.GetString($"imperial-malf-category-{category.ToString().ToLowerInvariant()}"), () =>
            {
                _category = current;
                if (_search.Text.Length > 0)
                    _search.Text = string.Empty;
                Rebuild();
            }, category == _category ? Theme.Selected : Tgui.Transparent, fluid: true);
            tab.TextLabel.HorizontalAlignment = HAlignment.Left;
            tab.MinHeight = 30;
            tabs.AddChild(tab);
        }

        var left = Tgui.VBox(6, Tgui.HBox(4, currency, compact), _search, tabs);
        left.MinWidth = 160;
        left.MaxWidth = 160;
        _root.AddChild(left);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(_items);
        _root.AddChild(scroll);
        RebuildItems();
    }

    private void RebuildItems()
    {
        if (_state is not { } state)
            return;

        _items.RemoveAllChildren();
        var search = _search.Text.Trim();
        var modules = _proto.EnumeratePrototypes<ImperialMalfModulePrototype>()
            .Where(m => search.Length > 0
                ? Loc.GetString(m.Name).Contains(search, StringComparison.OrdinalIgnoreCase)
                : m.Category == _category)
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Cost)
            .ToList();

        if (modules.Count == 0)
        {
            _items.AddChild(Tgui.NoticeBox(Loc.GetString(search.Length == 0 ? "imperial-malf-picker-empty" : "imperial-malf-picker-nothing"),
                theme: Theme));
            return;
        }

        var compact = _compact || search.Length > 0;
        foreach (var module in modules)
        {
            var bought = module.OnePurchase && state.Bought.Contains(module.ID);
            var price = state.Costs.GetValueOrDefault(module.ID, module.Cost);
            var disabled = bought || state.ProcessingTime < price || state.HackedApcs < module.MinimumApcs;
            var id = module.ID;
            var cost = Tgui.Button(Loc.GetString("imperial-malf-picker-cost", ("cost", price)), () => OnBuy?.Invoke(id),
                Theme.Primary, disabled: disabled, theme: Theme);

            var desc = Loc.GetString(module.Description);
            if (module.MinimumApcs > 0)
                desc += Loc.GetString("imperial-malf-picker-min-apcs", ("apcs", module.MinimumApcs));

            var icon = new TextureRect
            {
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = compact ? new Vector2(32, 32) : new Vector2(64, 64),
                SetSize = compact ? new Vector2(32, 32) : new Vector2(64, 64),
            };
            if (module.Icon != null)
                icon.Texture = _sprite.Frame0(module.Icon);

            Control body;
            if (compact)
            {
                var name = Tgui.Text(Loc.GetString(module.Name), bold: true);
                name.HorizontalExpand = true;
                name.VerticalAlignment = VAlignment.Center;
                name.ToolTip = desc;
                name.MouseFilter = MouseFilterMode.Stop;
                body = Tgui.HBox(6, name, cost);
            }
            else
            {
                body = Tgui.MakeSection(Loc.GetString(module.Name), cost, Tgui.Paragraph(desc, Theme.Label), Theme);
            }

            body.HorizontalExpand = true;
            _items.AddChild(Tgui.MakeSection(null, null, Tgui.HBox(6, icon, body), Theme));
        }
    }
}
