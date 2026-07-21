using Content.Shared.Imperial.Medieval.Ships.ShipBuyTerminal;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Ships.ShipBuyTerminal;

[UsedImplicitly]
public sealed class ShipBuyTerminalBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ShipBuyTerminalMenu? _menu;

    public ShipBuyTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ShipBuyTerminalMenu>();
        _menu.OnBuyOffer += (_, index) => SendMessage(new ShipBuyTerminalBuyMessage(index));
        _menu.OnWithdrawAttempt += (_, _, amount) => SendMessage(new ShipBuyTerminalWithdrawMessage(amount));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null)
            return;

        if (state is not ShipBuyTerminalUpdateState msg)
            return;

        _menu.UpdateState(msg);
    }
}
