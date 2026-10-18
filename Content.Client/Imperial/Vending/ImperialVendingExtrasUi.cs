using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Vending;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Vending;

/// <summary>Окно своего торгомата (vending/custom TG): товары владельца с ценами.</summary>
public sealed class ImperialCustomVendorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _header = default!;
    private BoxContainer _list = default!;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = EntMan.TryGetComponent(Owner, out MetaDataComponent? meta) ? meta.EntityName : string.Empty;
        _window.SetSize = new Vector2(431, 500);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        _header = new Label { Margin = new Thickness(4) };
        root.AddChild(_header);
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        root.AddChild(scroll);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialCustomVendorState s)
            return;

        _header.Text = s.Owner == string.Empty
            ? Loc.GetString("imperial-custom-vendor-ui-no-owner")
            : Loc.GetString("imperial-custom-vendor-ui-owner", ("owner", s.Owner), ("cash", s.Cash));

        _list.RemoveAllChildren();
        if (s.Items.Count == 0)
            _list.AddChild(new Label { Text = Loc.GetString("imperial-custom-vendor-ui-empty"), Margin = new Thickness(4) });

        foreach (var item in s.Items)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 1) };
            var view = new EntityPrototypeView { SetSize = new Vector2(32, 32) };
            if (item.Proto != string.Empty)
                view.SetPrototype(item.Proto);
            row.AddChild(view);
            row.AddChild(new Label { Text = item.Name, HorizontalExpand = true, ClipText = true, VerticalAlignment = Control.VAlignment.Center });

            var entry = item;
            if (s.IsOwner)
            {
                var price = new LineEdit { Text = item.Price.ToString(), MinWidth = 60 };
                row.AddChild(price);
                var set = new Button { Text = Loc.GetString("imperial-custom-vendor-ui-set") };
                set.OnPressed += _ =>
                {
                    if (int.TryParse(price.Text, out var value) && value >= 0)
                        SendMessage(new ImperialCustomVendorPriceMessage(entry.Item, value));
                };
                row.AddChild(set);
            }

            var buy = new Button
            {
                Text = s.IsOwner ? Loc.GetString("imperial-custom-vendor-ui-take") : Loc.GetString("imperial-vending-ui-price", ("price", item.Price)),
                MinWidth = 80,
                Disabled = !s.IsOwner && (s.Owner == string.Empty || item.Price > s.Cash),
            };
            buy.OnPressed += _ => SendMessage(new ImperialCustomVendorBuyMessage(entry.Item));
            row.AddChild(buy);
            _list.AddChild(row);
        }
    }
}

/// <summary>Окно аплинка чёрного рынка (tgui BlackMarketUplink): категории, товары, способ доставки.</summary>
public sealed class ImperialMarketBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _header = default!;
    private BoxContainer _categories = default!;
    private BoxContainer _list = default!;
    private OptionButton _shipping = default!;
    private ImperialMarketState? _state;
    private string? _category;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-market-ui-title");
        _window.SetSize = new Vector2(600, 550);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _header = new Label { HorizontalExpand = true, Margin = new Thickness(4) };
        top.AddChild(_header);
        top.AddChild(new Label { Text = Loc.GetString("imperial-market-ui-shipping"), VerticalAlignment = Control.VAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _shipping = new OptionButton();
        _shipping.AddItem(Loc.GetString("imperial-market-ui-launch"), (int) ImperialMarketShipping.Launch);
        _shipping.AddItem(Loc.GetString("imperial-market-ui-teleport"), (int) ImperialMarketShipping.Teleport);
        _shipping.OnItemSelected += args =>
        {
            _shipping.SelectId(args.Id);
            Rebuild();
        };
        top.AddChild(_shipping);
        root.AddChild(top);

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, VerticalExpand = true };
        _categories = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 130 };
        body.AddChild(_categories);
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        body.AddChild(scroll);
        root.AddChild(body);
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialMarketState s)
            return;

        _state = s;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state is not { } s)
            return;

        _header.Text = s.Holder == string.Empty
            ? Loc.GetString("imperial-market-ui-no-account")
            : Loc.GetString("imperial-market-ui-account", ("holder", s.Holder), ("balance", s.Balance));

        var categories = s.Items.Select(i => i.Category).Distinct().ToList();
        if (_category == null || !categories.Contains(_category))
            _category = categories.FirstOrDefault();

        _categories.RemoveAllChildren();
        foreach (var category in categories)
        {
            var button = new Button { Text = category, ToggleMode = true, Pressed = category == _category };
            var selected = category;
            button.OnPressed += _ =>
            {
                _category = selected;
                Rebuild();
            };
            _categories.AddChild(button);
        }

        var shipping = (ImperialMarketShipping) _shipping.SelectedId;
        var fee = shipping == ImperialMarketShipping.Teleport ? s.TeleportFee : s.LaunchFee;
        _list.RemoveAllChildren();
        if (s.Items.Count == 0)
            _list.AddChild(new Label { Text = Loc.GetString("imperial-market-ui-empty") });

        foreach (var item in s.Items.Where(i => i.Category == _category))
        {
            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 0, 0, 6) };
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            var view = new EntityPrototypeView { SetSize = new Vector2(32, 32) };
            view.SetPrototype(item.Item);
            row.AddChild(view);
            row.AddChild(new Label { Text = item.Name, HorizontalExpand = true, ClipText = true, VerticalAlignment = Control.VAlignment.Center });
            row.AddChild(new Label { Text = Loc.GetString("imperial-market-ui-stock", ("stock", item.Stock)), VerticalAlignment = Control.VAlignment.Center, Margin = new Thickness(4, 0) });
            var price = item.Price + fee;
            var buy = new Button { Text = Loc.GetString("imperial-vending-ui-price", ("price", price)), MinWidth = 90, Disabled = price > s.Balance || s.Holder == string.Empty };
            var id = item.Id;
            buy.OnPressed += _ => SendMessage(new ImperialMarketBuyMessage(id, (ImperialMarketShipping) _shipping.SelectedId));
            row.AddChild(buy);
            box.AddChild(row);
            box.AddChild(new RichTextLabel { Text = item.Description, Margin = new Thickness(36, 0, 0, 0) });
            _list.AddChild(box);
        }
    }
}

/// <summary>Цвет использованной полоски pH (CONVERT_PH_TO_COLOR) и состояние книжки.</summary>
public sealed class ImperialPhPaperVisualizerSystem : VisualizerSystem<ImperialPhPaperComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ImperialPhPaperComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !AppearanceSystem.TryGetData<string>(uid, ImperialPhBookletVisuals.State, out var hex, args.Component))
            return;

        if (Color.TryFromHex(hex) is { } color)
            SpriteSystem.SetColor((uid, args.Sprite), color);
    }
}
