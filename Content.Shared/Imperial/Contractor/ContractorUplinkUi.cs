using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

[Serializable, NetSerializable]
public enum ContractorUplinkUiKey
{
    Key,
}

/// <summary>CONTRACT_STATUS_* из SS13.</summary>
[Serializable, NetSerializable]
public enum ContractorContractStatus : byte
{
    Inactive = 1,
    Active = 2,
    Extracting = 4,
    Complete = 5,
    Aborted = 6,
}

/// <summary>Контракт в ui_data программы SyndicateContractor.</summary>
[Serializable, NetSerializable]
public sealed class ContractorContractData
{
    public int Id { get; }
    public string Target { get; }
    public string TargetRank { get; }
    public int Payout { get; }
    public int PayoutBonus { get; }
    public string Dropoff { get; }
    public ContractorContractStatus Status { get; }
    public string Message { get; }

    public ContractorContractData(int id, string target, string targetRank, int payout, int payoutBonus, string dropoff,
        ContractorContractStatus status, string message)
    {
        Id = id;
        Target = target;
        TargetRank = targetRank;
        Payout = payout;
        PayoutBonus = payoutBonus;
        Dropoff = dropoff;
        Status = status;
        Message = message;
    }
}

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

/// <summary>PRG_login.</summary>
[Serializable, NetSerializable]
public sealed class ContractorLoginMessage : BoundUserInterfaceMessage;

/// <summary>PRG_set_first_load_finished.</summary>
[Serializable, NetSerializable]
public sealed class ContractorFirstLoadFinishedMessage : BoundUserInterfaceMessage;

/// <summary>PRG_toggle_info.</summary>
[Serializable, NetSerializable]
public sealed class ContractorToggleInfoMessage : BoundUserInterfaceMessage;

/// <summary>PRG_clear_error.</summary>
[Serializable, NetSerializable]
public sealed class ContractorClearErrorMessage : BoundUserInterfaceMessage;

/// <summary>PRG_contract-accept.</summary>
[Serializable, NetSerializable]
public sealed class ContractorAcceptContractMessage : BoundUserInterfaceMessage
{
    public int ContractId { get; }

    public ContractorAcceptContractMessage(int contractId)
    {
        ContractId = contractId;
    }
}

/// <summary>PRG_contract_abort.</summary>
[Serializable, NetSerializable]
public sealed class ContractorAbortContractMessage : BoundUserInterfaceMessage;

/// <summary>PRG_call_extraction.</summary>
[Serializable, NetSerializable]
public sealed class ContractorCallExtractionMessage : BoundUserInterfaceMessage;

/// <summary>PRG_redeem_TC.</summary>
[Serializable, NetSerializable]
public sealed class ContractorRedeemTcMessage : BoundUserInterfaceMessage;
