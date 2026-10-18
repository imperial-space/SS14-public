using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using Content.Shared.Imperial.Contractor;

namespace Content.Client.Imperial.Contractor;

public sealed class ContractorUplinkBoundUserInterface : BoundUserInterface
{
    private ContractorUplinkWindow? _window;

    public ContractorUplinkBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ContractorUplinkWindow>();
        _window.AcceptContract += (id, difficulty) => SendMessage(new ContractorAcceptContractMessage(id, difficulty));
        _window.DeclineContract += id => SendMessage(new ContractorDeclineContractMessage(id));
        _window.BuyRequisition += id => SendMessage(new ContractorBuyRequisitionMessage(id));
        _window.OpenPortal += () => SendMessage(new ContractorOpenPortalMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ContractorUplinkBoundUserInterfaceState contractorState)
            _window?.UpdateState(contractorState);
    }
}