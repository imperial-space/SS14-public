using System.Linq;
using System.Numerics;
using Content.Client.VendingMachines;
using Content.Shared.Imperial.Vending;
using Content.Shared.VendingMachines;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Vending;

/// <summary>
/// Окно торгомата 1:1 с tgui/interfaces/Vending.tsx: карточка покупателя (UserDetails), реклама,
/// раздел «Товары» с деньгами, поиском и переключателем сетка/список, выбор категории внизу.
/// </summary>
public sealed class ImperialVendingBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly Color InfoColor = Color.FromHex("#2b5b8f");
    private static readonly Color WarningColor = Color.FromHex("#bb9b68");
    private static readonly Color SectionColor = Color.FromHex("#1a1a1a");
    private static readonly Color TitleColor = Color.FromHex("#252525");
    private static readonly Color GoodColor = Color.FromHex("#5baa27");
    private static readonly Color GoldColor = Color.FromHex("#f3b22f");
    private static readonly Color RedColor = Color.FromHex("#db2828");
    private static readonly Color YellowColor = Color.FromHex("#fbd608");

    /// <summary>layoutMode из localState: сетка по умолчанию, запоминается между окнами.</summary>
    private static bool _gridMode = true;

    private RefreshWindow? _window;
    private BoxContainer _notices = default!;
    private Label _cash = default!;
    private LineEdit _search = default!;
    private Button _layout = default!;
    private BoxContainer _products = default!;
    private ScrollContainer _scroll = default!;
    private PanelContainer _categoryPanel = default!;
    private BoxContainer _categories = default!;

    private ImperialVendingUserState? _state;
    private string? _selectedCategory;
    private string _signature = string.Empty;

    private sealed class RefreshWindow : DefaultWindow
    {
        public Action? OnRefresh;
        private float _accumulator;

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            _accumulator += args.DeltaSeconds;
            if (_accumulator < 0.25f)
                return;
            _accumulator = 0;
            OnRefresh?.Invoke();
        }
    }

    private sealed record Product(VendingMachineInventoryEntry Entry, string Name, string Category, int Price);

    private static PanelContainer Panel(Color color, Control child, Thickness margin)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
            Margin = margin,
        };
        panel.AddChild(child);
        return panel;
    }

    protected override void Open()
    {
        base.Open();
        IoCManager.InjectDependencies(this);

        _window = this.CreateWindow<RefreshWindow>();
        _window.Title = EntMan.TryGetComponent(Owner, out MetaDataComponent? meta) ? meta.EntityName : string.Empty;
        _window.SetSize = new Vector2(431, 635);
        _window.OnRefresh = () => Rebuild(false);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };

        _notices = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(_notices);

        // Section title="Products": деньги, поиск, вид.
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label { Text = Loc.GetString("imperial-vending-ui-products"), HorizontalExpand = true, VerticalAlignment = Control.VAlignment.Center, FontColorOverride = Color.White });
        _cash = new Label { FontColorOverride = GoodColor, VerticalAlignment = Control.VAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        header.AddChild(_cash);
        _search = new LineEdit { PlaceHolder = Loc.GetString("imperial-vending-search"), MinWidth = 120 };
        _search.OnTextChanged += _ => Rebuild(true);
        header.AddChild(_search);
        _layout = new Button { MinWidth = 28, Margin = new Thickness(4, 0, 0, 0) };
        _layout.OnPressed += _ =>
        {
            _gridMode = !_gridMode;
            Rebuild(true);
        };
        header.AddChild(_layout);
        root.AddChild(Panel(TitleColor, header, new Thickness(0)));

        _products = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        _scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _scroll.AddChild(_products);
        var body = Panel(SectionColor, _scroll, new Thickness(0, 0, 0, 4));
        body.VerticalExpand = true;
        root.AddChild(body);

        // CategorySelector.
        _categories = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var categoryScroll = new ScrollContainer { VScrollEnabled = false, MinHeight = 34 };
        categoryScroll.AddChild(_categories);
        var categoryRoot = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        categoryRoot.AddChild(new Label { Text = Loc.GetString("imperial-vending-ui-categories"), FontColorOverride = Color.White });
        categoryRoot.AddChild(categoryScroll);
        _categoryPanel = Panel(SectionColor, categoryRoot, new Thickness(0));
        root.AddChild(_categoryPanel);

        _window.Contents.AddChild(root);
        Rebuild(true);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialVendingUserState user)
            return;

        _state = user;
        Rebuild(true);
    }

    private bool Free(ImperialVendingComponent? prices) => prices == null || prices.Free || _state?.Free == true;

    /// <summary>Категория и цена товара; скидка отдела (jobDiscount) только для не премиум-товаров.</summary>
    private Product MakeProduct(ImperialVendingComponent? prices, VendingMachineInventoryEntry entry)
    {
        var name = _proto.TryIndex<EntityPrototype>(entry.ID, out var proto) ? proto.Name : entry.ID;
        var key = ImperialVendingComponent.Key(entry.Type, entry.ID);
        var premium = prices?.Premium.Contains(key) == true;

        var category = entry.Type == InventoryType.Contraband ? Loc.GetString("imperial-vending-ui-contraband")
            : premium ? Loc.GetString("imperial-vending-ui-premium")
            : prices != null && prices.Categories.TryGetValue(key, out var cat) ? cat
            : Loc.GetString("imperial-vending-ui-products");

        var price = 0;
        if (!Free(prices))
        {
            price = prices!.PriceOf(entry.Type, entry.ID);
            if (!premium && price > 0 && prices.Department is { } department && _state?.Department == department.Id)
                price = Math.Max((int) Math.Round(price * 0.2f), 1);
        }

        return new Product(entry, name, category, price);
    }

    private void Rebuild(bool force)
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out VendingMachineComponent? vending))
            return;

        EntMan.TryGetComponent(Owner, out ImperialVendingComponent? prices);
        var inventory = EntMan.System<VendingMachineSystem>().GetAllInventory(Owner, vending);
        var signature = string.Join(";", inventory.Select(e => $"{e.ID}:{e.Amount}")) + vending.Ejecting;
        if (!force && signature == _signature)
            return;
        _signature = signature;

        var free = Free(prices);
        var cash = _state?.Cash ?? 0;

        // UserDetails и реклама.
        _notices.RemoveAllChildren();
        if (!free)
        {
            var user = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            user.AddChild(new Label { Text = "▣", Margin = new Thickness(0, 0, 6, 0), FontColorOverride = Color.White });
            user.AddChild(new Label
            {
                Text = _state?.HasUser == true
                    ? $"{(_state.Holder == string.Empty ? Loc.GetString("imperial-vending-ui-unknown") : _state.Holder)} | {(_state.Job == string.Empty ? Loc.GetString("imperial-vending-ui-unemployed") : _state.Job)}"
                    : Loc.GetString("imperial-vending-ui-no-id"),
                FontColorOverride = Color.White,
                ClipText = true,
                HorizontalExpand = true,
            });
            _notices.AddChild(Panel(InfoColor, user, new Thickness(0, 0, 0, 4)));
        }

        if (!string.IsNullOrEmpty(_state?.Ad))
            _notices.AddChild(Panel(WarningColor, new Label { Text = _state.Ad, FontColorOverride = Color.Black, ClipText = true }, new Thickness(0, 0, 0, 4)));

        _cash.Visible = !free;
        _cash.Text = Loc.GetString("imperial-vending-ui-cash", ("cash", cash));
        _layout.Text = _gridMode ? "☰" : "▦";
        _layout.ToolTip = Loc.GetString(_gridMode ? "imperial-vending-ui-list" : "imperial-vending-ui-grid");

        var products = inventory.Select(e => MakeProduct(prices, e)).ToList();
        var categories = products.Select(p => p.Category).Distinct().ToList();
        if (_selectedCategory == null || !categories.Contains(_selectedCategory))
            _selectedCategory = categories.FirstOrDefault();

        // Поиск от двух символов по всем категориям, иначе — выбранная категория.
        var search = _search.Text.Trim();
        var searching = search.Length >= 2;
        var shown = searching
            ? products.Where(p => p.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            : categories.Count > 1 ? products.Where(p => p.Category == _selectedCategory) : products;

        _products.RemoveAllChildren();
        GridContainer? grid = null;
        if (_gridMode)
        {
            grid = new GridContainer { Columns = 4, HorizontalExpand = true };
            _products.AddChild(grid);
        }

        foreach (var product in shown)
        {
            var disabled = product.Entry.Amount == 0 || vending.Ejecting || !free && product.Price > cash;
            if (grid != null)
                grid.AddChild(GridTile(product, disabled, free));
            else
                _products.AddChild(ListRow(product, disabled, free));
        }

        _categoryPanel.Visible = !searching && categories.Count > 1;
        _categories.RemoveAllChildren();
        foreach (var category in categories)
        {
            var button = new Button
            {
                Text = category,
                ToggleMode = true,
                Pressed = category == _selectedCategory,
                Margin = new Thickness(0, 0, 3, 0),
            };
            if (category == Loc.GetString("imperial-vending-ui-contraband"))
                button.ModulateSelfOverride = RedColor;
            else if (category == Loc.GetString("imperial-vending-ui-premium"))
                button.ModulateSelfOverride = YellowColor;

            var selected = category;
            button.OnPressed += _ =>
            {
                _selectedCategory = selected;
                _scroll.SetScrollValue(Vector2.Zero);
                Rebuild(true);
            };
            _categories.AddChild(button);
        }
    }

    private string PriceText(Product product, bool free)
    {
        return free || product.Price == 0
            ? Loc.GetString("imperial-vending-ui-free-item")
            : Loc.GetString("imperial-vending-ui-price", ("price", product.Price));
    }

    private void Vend(Product product)
    {
        SendMessage(new ImperialVendingBuyMessage(product.Entry.Type, product.Entry.ID));
    }

    private Color? NameColor(Product product) => product.Entry.Type == InventoryType.Contraband ? RedColor : null;

    /// <summary>Режим сетки: значок с подписью, внизу цена и остаток.</summary>
    private Control GridTile(Product product, bool disabled, bool free)
    {
        var button = new Button { Disabled = disabled, ToolTip = product.Name, MinSize = new Vector2(94, 0), Margin = new Thickness(1) };
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var view = new EntityPrototypeView { SetSize = new Vector2(64, 64), Scale = new Vector2(2, 2), HorizontalAlignment = Control.HAlignment.Center };
        view.SetPrototype(product.Entry.ID);
        box.AddChild(view);
        box.AddChild(new Label { Text = product.Name, ClipText = true, HorizontalAlignment = Control.HAlignment.Center, FontColorOverride = NameColor(product) });

        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        bottom.AddChild(new Label { Text = PriceText(product, free), FontColorOverride = GoldColor, HorizontalExpand = true, ClipText = true });
        bottom.AddChild(new Label { Text = $"x{product.Entry.Amount}", FontColorOverride = product.Entry.Amount == 0 ? RedColor : Color.LightGray });
        box.AddChild(bottom);

        button.AddChild(box);
        button.OnPressed += _ => Vend(product);
        return button;
    }

    /// <summary>Режим списка: значок, название, остаток и кнопка с ценой.</summary>
    private Control ListRow(Product product, bool disabled, bool free)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 1) };
        var view = new EntityPrototypeView { SetSize = new Vector2(32, 32) };
        view.SetPrototype(product.Entry.ID);
        row.AddChild(view);
        row.AddChild(new Label
        {
            Text = product.Name,
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = NameColor(product),
        });
        row.AddChild(new Label
        {
            Text = Loc.GetString("imperial-vending-ui-left", ("amount", product.Entry.Amount)),
            MinWidth = 60,
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = product.Entry.Amount == 0 ? RedColor : Color.LightGray,
        });
        var buy = new Button { Text = PriceText(product, free), MinWidth = 80, Disabled = disabled };
        buy.OnPressed += _ => Vend(product);
        row.AddChild(buy);
        return row;
    }
}
