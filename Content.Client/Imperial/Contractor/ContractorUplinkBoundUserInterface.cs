using Robust.Client.UserInterface;
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
        _window.Login += () => SendMessage(new ContractorLoginMessage());
        _window.FirstLoadFinished += () => SendMessage(new ContractorFirstLoadFinishedMessage());
        _window.ToggleInfo += () => SendMessage(new ContractorToggleInfoMessage());
        _window.ClearError += () => SendMessage(new ContractorClearErrorMessage());
        _window.AcceptContract += id => SendMessage(new ContractorAcceptContractMessage(id));
        _window.AbortContract += () => SendMessage(new ContractorAbortContractMessage());
        _window.CallExtraction += () => SendMessage(new ContractorCallExtractionMessage());
        _window.RedeemTc += () => SendMessage(new ContractorRedeemTcMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ContractorUplinkBoundUserInterfaceState contractorState)
            _window?.UpdateState(contractorState);
    }
}
