using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Lavaland.OrePoints.MiningVoucher;

/// <summary>show_radial_menu() voucher_redeemer: иконка набора и описание во всплывающей подсказке.</summary>
[UsedImplicitly]
public sealed class MiningVoucherBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public MiningVoucherBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        var buttons = MiningVoucherKits.All.Select((kit, i) => (RadialMenuOptionBase) new RadialMenuActionOption<int>(Pick, i)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(kit.Icon),
            ToolTip = $"{Loc.GetString(kit.Name)}\n{Loc.GetString(kit.Description)}",
        }).ToList();
        _menu.SetButtons(buttons);
        _menu.OpenOverMouseScreenPosition();
    }

    private void Pick(int index)
    {
        SendMessage(new MiningVoucherSelectKitMessage(index));
    }
}
