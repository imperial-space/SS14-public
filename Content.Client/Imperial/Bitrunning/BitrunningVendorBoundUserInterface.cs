using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

[UsedImplicitly]
public sealed class BitrunningVendorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BitrunningVendorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BitrunningVendorWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BitrunningVendorState vendor)
            _window?.Update(vendor);
    }
}

/// <summary>Торгомат NexaCache 1 в 1 с tgui ProduceConsole: вкладки «Покупки» и «Оплата», корзина до 20 штук.</summary>
public sealed class BitrunningVendorWindow : DefaultWindow
{
    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private const int ButtonHeight = 32;
    private const int MaxAmount = 20;

    private readonly BoxContainer _root = Tgui.VBox(6);
    private readonly LineEdit _search = new() { MinWidth = 150 };
    private readonly BoxContainer _goods = Tgui.VBox(0);

    private BitrunningVendorState? _state;
    private bool _checkout;
    /// <summary>condensedAtom: без иконок товаров.</summary>
    private bool _condensed;
    private string? _category;

    public BitrunningVendorWindow()
    {
        MinSize = SetSize = new Vector2(500, 400);
        _root.Margin = new Thickness(6);
        _root.VerticalExpand = true;
        _search.PlaceHolder = Loc.GetString("bitrunning-vendor-search");
        _search.OnTextChanged += _ => RebuildGoods();
        Contents.AddChild(Tgui.Window(_root, scrollable: false));
    }

    private void Send(BoundUserInterfaceMessage message)
    {
        OnMessage?.Invoke(message);
    }

    private static string Credit => Loc.GetString("bitrunning-vendor-credit");

    private int Amount(string id)
    {
        return _state?.Cart.GetValueOrDefault(id) ?? 0;
    }

    private void SetAmount(string id, int amount)
    {
        Send(new BitrunningVendorCartMessage(id, Math.Clamp(amount, 0, MaxAmount)));
    }

    public void Update(BitrunningVendorState state)
    {
        _state = state;
        _category ??= state.Categories.FirstOrDefault();
        SetSize = new Vector2(Math.Max(state.Categories.Count * 125, 500), 400);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state is not { } state)
            return;

        _search.Orphan();
        _goods.Orphan();
        _root.RemoveAllChildren();

        // OrderSent: Dimmer с самолётом, пока торгомат на откате.
        if (!state.OffCooldown)
        {
            var sent = Tgui.VBox(8,
                BitrunningTgui.Icon("plane-arrival", Tgui.Green, 80),
                Tgui.Text(Loc.GetString("bitrunning-vendor-order-sent"), Tgui.Green, 18, align: HAlignment.Center));
            sent.HorizontalAlignment = HAlignment.Center;
            sent.VerticalAlignment = VAlignment.Center;
            sent.VerticalExpand = true;
            ((Label) sent.GetChild(0)).HorizontalAlignment = HAlignment.Center;
            _root.AddChild(sent);
            return;
        }

        _root.AddChild(Header(state));

        var body = _checkout ? CheckoutTab(state) : ShoppingTab(state);
        body.VerticalExpand = true;
        _root.AddChild(body);
    }

    private Control Header(BitrunningVendorState state)
    {
        var shopping = BitrunningTgui.Button(Loc.GetString("bitrunning-vendor-shopping"), "cart-plus", Tgui.Green, () =>
        {
            _checkout = false;
            Rebuild();
        }, fluid: true);
        shopping.MinHeight = ButtonHeight;
        shopping.SizeFlagsStretchRatio = 3;

        var checkout = BitrunningTgui.Button(Loc.GetString("bitrunning-vendor-checkout"), "dollar-sign", Tgui.Green, () =>
        {
            _checkout = true;
            Rebuild();
        }, fluid: true);
        checkout.MinHeight = ButtonHeight;
        checkout.SizeFlagsStretchRatio = 1;

        var condense = Tgui.Button(Loc.GetString(_condensed ? "bitrunning-vendor-expand" : "bitrunning-vendor-condense"), () =>
        {
            _condensed = !_condensed;
            Rebuild();
        }, _condensed ? Tgui.Green : Tgui.Red);

        var spacer = new Control { HorizontalExpand = true };
        var points = Tgui.Text($"{state.Points} {Credit}");
        points.VerticalAlignment = VAlignment.Center;

        return Tgui.MakeSection(null, null, Tgui.VBox(6,
            Tgui.HBox(6, shopping, checkout),
            Tgui.HBox(6, condense, spacer, points)));
    }

    private Control ShoppingTab(BitrunningVendorState state)
    {
        var tabs = new TguiTabs { HorizontalExpand = true };
        foreach (var category in state.Categories)
        {
            var name = category;
            tabs.AddTab(category, null, category == _category, () =>
            {
                _category = name;
                if (_search.Text.Length > 0)
                    _search.Text = string.Empty;
                RebuildGoods();
            });
        }

        foreach (var tab in tabs.Children)
        {
            tab.HorizontalExpand = true;
        }

        _search.VerticalAlignment = VAlignment.Center;
        var top = Tgui.MakeSection(null, null, Tgui.HBox(6, tabs, _search));

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_goods);
        var list = Tgui.MakeSection(null, null, scroll);
        list.VerticalExpand = true;

        RebuildGoods();
        var box = Tgui.VBox(0, top, list);
        return box;
    }

    /// <summary>goods: поиск по имени по всем категориям, иначе — товары выбранной категории.</summary>
    private void RebuildGoods()
    {
        if (_state is not { } state)
            return;

        _goods.RemoveAllChildren();
        var search = _search.Text.Trim();
        var goods = search.Length > 0
            ? state.Entries.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            : state.Entries.Where(e => e.Category == _category);

        foreach (var item in goods)
        {
            var row = Tgui.HBox(6);
            row.Margin = new Thickness(0, 2);

            if (!_condensed)
            {
                var view = new EntityPrototypeView { MinSize = new Vector2(36, 36), SetSize = new Vector2(36, 36) };
                view.SetPrototype(item.Proto);
                row.AddChild(view);
            }

            var name = Tgui.Text(Capitalize(item.Name), Tgui.Label);
            name.HorizontalExpand = true;
            name.VerticalAlignment = VAlignment.Center;
            name.ClipText = true;
            row.AddChild(name);

            var info = BitrunningTgui.Button(string.Empty, "info", Tgui.Transparent, tooltip: item.Description);
            info.VerticalAlignment = VAlignment.Center;
            row.AddChild(info);

            var cost = Tgui.Text($"{item.Cost} {Credit}", size: 10);
            cost.VerticalAlignment = VAlignment.Center;
            cost.ToolTip = Loc.GetString("bitrunning-vendor-cost-tooltip", ("cost", item.Cost), ("credit", Credit));
            cost.MouseFilter = MouseFilterMode.Stop;
            row.AddChild(cost);

            var id = item.Id;
            var minus = BitrunningTgui.Button(string.Empty, "minus", onPressed: () => SetAmount(id, Amount(id) - 1));
            var plus = BitrunningTgui.Button(string.Empty, "plus", onPressed: () => SetAmount(id, Amount(id) + 1));
            minus.VerticalAlignment = plus.VerticalAlignment = VAlignment.Center;
            row.AddChild(minus);
            row.AddChild(plus);
            row.AddChild(AmountInput(id, MaxAmount));

            _goods.AddChild(row);
            _goods.AddChild(RowLine());
        }
    }

    private Control AmountInput(string id, int max)
    {
        var input = new TguiNumberInput(41)
        {
            MinValue = 0,
            MaxValue = max,
            Step = 1,
            Value = Amount(id),
            VerticalAlignment = VAlignment.Center,
        };
        input.OnChange += value => SetAmount(id, (int) value);
        return input;
    }

    private static Control RowLine()
    {
        return new PanelContainer
        {
            MinHeight = 1,
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = Color.FromHex("#333333") },
        };
    }

    private Control CheckoutTab(BitrunningVendorState state)
    {
        var totalCargo = (int) MathF.Floor(state.TotalCost * state.CargoCostMultiplier);
        var list = Tgui.VBox(2);
        list.AddChild(Tgui.Text(Loc.GetString("bitrunning-vendor-list"), Tgui.Label, bold: true, align: HAlignment.Center));

        var checkout = state.Entries.Where(e => Amount(e.Id) > 0).ToList();
        if (checkout.Count == 0)
        {
            var nothing = Tgui.Text(Loc.GetString("bitrunning-vendor-nothing"), size: 40, align: HAlignment.Center);
            nothing.Margin = new Thickness(0, 40, 0, 0);
            list.AddChild(nothing);
            var hint = Tgui.Text(Loc.GetString("bitrunning-vendor-nothing-hint"), size: 15, align: HAlignment.Center);
            hint.Margin = new Thickness(0, 16, 0, 0);
            list.AddChild(hint);
        }

        foreach (var item in checkout)
        {
            var name = Tgui.Text(Capitalize(item.Name));
            name.VerticalAlignment = VAlignment.Center;

            var desc = Tgui.Text($"\"{item.Description}\"", Tgui.Label, 10);
            desc.HorizontalExpand = true;
            desc.ClipText = true;
            desc.VerticalAlignment = VAlignment.Center;

            var cost = Tgui.Text($"{item.Cost} {Credit}", size: 10);
            cost.VerticalAlignment = VAlignment.Center;
            cost.ToolTip = Loc.GetString("bitrunning-vendor-cost-tooltip", ("cost", item.Cost), ("credit", Credit));
            cost.MouseFilter = MouseFilterMode.Stop;

            list.AddChild(Tgui.HBox(6, name, desc, cost, AmountInput(item.Id, item.Cost > 10 ? 50 : 10)));
            list.AddChild(RowLine());
        }

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(list);
        var top = Tgui.MakeSection(null, null, scroll);
        top.VerticalExpand = true;

        var purchase = BitrunningTgui.Button(Loc.GetString("bitrunning-vendor-purchase", ("cost", $"{totalCargo} {Credit}")), "plane-departure",
            onPressed: () => Send(new BitrunningVendorPurchaseMessage(false)),
            disabled: totalCargo < state.CargoValue,
            tooltip: totalCargo < state.CargoValue
                ? Loc.GetString("bitrunning-vendor-min-total", ("value", state.CargoValue))
                : Loc.GetString("bitrunning-vendor-purchase-tooltip"),
            fluid: true);

        var expressCost = (int) MathF.Floor(state.TotalCost * state.ExpressCostMultiplier);
        var express = BitrunningTgui.Button(Loc.GetString("bitrunning-vendor-express", ("cost", $"{expressCost} {Credit}")), "parachute-box",
            Tgui.Yellow,
            () => Send(new BitrunningVendorPurchaseMessage(true)),
            disabled: state.TotalCost <= 0,
            tooltip: state.TotalCost <= 0
                ? Loc.GetString("bitrunning-vendor-need-item")
                : Loc.GetString("bitrunning-vendor-express-tooltip"),
            fluid: true);
        if (state.TotalCost > 0)
            express.TextLabel.FontColorOverride = Color.Black;

        var bottom = Tgui.MakeSection(null, null, Tgui.HBox(6, purchase, express));
        return Tgui.VBox(4, top, bottom);
    }

    private static string Capitalize(string text)
    {
        return text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];
    }
}
