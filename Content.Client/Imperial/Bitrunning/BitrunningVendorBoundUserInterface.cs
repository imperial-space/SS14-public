using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Bitrunning;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

/// <summary>Окно торгомата NexaCache: товары за очки битраннинга, экспресс или грузовым рейсом.</summary>
public sealed class BitrunningVendorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private BitrunningVendorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BitrunningVendorWindow>();
        _window.OnBuy += (id, express) => SendMessage(new BitrunningVendorBuyMessage(id, express));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BitrunningVendorState vendor)
            _window?.Update(vendor);
    }
}

public sealed class BitrunningVendorWindow : DefaultWindow
{
    public event Action<string, bool>? OnBuy;

    private readonly Label _holder = new();
    private readonly Label _points = new();
    private readonly Label _message = new() { FontColorOverride = Color.Khaki };
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

    public BitrunningVendorWindow()
    {
        Title = Loc.GetString("bitrunning-vendor-title");
        MinSize = new Vector2(520, 560);
        SetSize = new Vector2(560, 640);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        root.AddChild(_holder);
        root.AddChild(_points);
        root.AddChild(_message);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void Update(BitrunningVendorState state)
    {
        _holder.Text = state.Holder == null
            ? Loc.GetString("bitrunning-vendor-no-account")
            : Loc.GetString("bitrunning-vendor-holder", ("holder", state.Holder));
        _points.Text = Loc.GetString("bitrunning-vendor-points", ("points", state.Points));
        _message.Text = state.Message ?? string.Empty;

        _list.RemoveAllChildren();
        foreach (var group in state.Entries.GroupBy(e => e.Category))
        {
            _list.AddChild(new Label
            {
                Text = Loc.GetString($"bitrunning-vendor-category-{group.Key}"),
                StyleClasses = { "LabelHeading" },
            });

            foreach (var entry in group)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
                var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
                info.AddChild(new Label { Text = entry.Name });
                info.AddChild(new RichTextLabel { Text = entry.Description });
                row.AddChild(info);

                var express = new Button
                {
                    Text = Loc.GetString("bitrunning-vendor-express", ("cost", entry.Cost)),
                    Disabled = state.Holder == null || state.Points < entry.Cost,
                };
                var cargo = new Button
                {
                    Text = Loc.GetString("bitrunning-vendor-cargo", ("cost", entry.CargoCost)),
                    Disabled = state.Holder == null || state.Points < entry.CargoCost,
                };
                var id = entry.Id;
                express.OnPressed += _ => OnBuy?.Invoke(id, true);
                cargo.OnPressed += _ => OnBuy?.Invoke(id, false);
                row.AddChild(express);
                row.AddChild(cargo);
                _list.AddChild(row);
            }
        }
    }
}
