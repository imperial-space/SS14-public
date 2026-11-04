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
