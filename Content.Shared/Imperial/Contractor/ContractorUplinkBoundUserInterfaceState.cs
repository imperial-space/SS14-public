using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

/// <summary>ui_data() программы contract_uplink.</summary>
[Serializable, NetSerializable]
public sealed class ContractorUplinkBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool FirstLoad { get; }
    public bool LoggedIn { get; }
    public bool InfoScreen { get; }
    public string Error { get; }
    public bool OngoingContract { get; }
    public bool ExtractionEnroute { get; }
    public string DropoffDirection { get; }
    public int RedeemableTc { get; }
    public int EarnedTc { get; }
    public int ContractsCompleted { get; }
    public ContractorContractData[] Contracts { get; }

    public ContractorUplinkBoundUserInterfaceState(bool firstLoad, bool loggedIn, bool infoScreen, string error,
        bool ongoingContract, bool extractionEnroute, string dropoffDirection, int redeemableTc, int earnedTc,
        int contractsCompleted, ContractorContractData[] contracts)
    {
        FirstLoad = firstLoad;
        LoggedIn = loggedIn;
        InfoScreen = infoScreen;
        Error = error;
        OngoingContract = ongoingContract;
        ExtractionEnroute = extractionEnroute;
        DropoffDirection = dropoffDirection;
        RedeemableTc = redeemableTc;
        EarnedTc = earnedTc;
        ContractsCompleted = contractsCompleted;
        Contracts = contracts;
    }
}
