using Content.Shared.FixedPoint;
using Content.Shared.UserInterface;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

[Serializable, NetSerializable]
public enum ContractorUplinkUiKey
{
    Key,
}

[Serializable, NetSerializable]
public enum ContractorDifficulty
{
    Easy,
    Medium,
    Hard,
}

[Serializable, NetSerializable]
public enum ContractorContractStage
{
    None,
    AwaitingBeacon,
    PortalReady,
}

[Serializable, NetSerializable]
public sealed class ContractorContractOfferData
{
    public string Id { get; }
    public string TargetName { get; }
    public string Job { get; }
    public ContractorContractOptionData[] Options { get; }

    public ContractorContractOfferData(
        string id,
        string targetName,
        string job,
        ContractorContractOptionData[] options)
    {
        Id = id;
        TargetName = targetName;
        Job = job;
        Options = options;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorContractOptionData
{
    public ContractorDifficulty Difficulty { get; }
    public string BeaconLabel { get; }
    public string Reason { get; }
    public FixedPoint2 AlivePayout { get; }
    public FixedPoint2 DeadPayout { get; }

    public ContractorContractOptionData(
        ContractorDifficulty difficulty,
        string beaconLabel,
        string reason,
        FixedPoint2 alivePayout,
        FixedPoint2 deadPayout)
    {
        Difficulty = difficulty;
        BeaconLabel = beaconLabel;
        Reason = reason;
        AlivePayout = alivePayout;
        DeadPayout = deadPayout;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorActiveContractData
{
    public string Id { get; }
    public string TargetName { get; }
    public string Job { get; }
    public string BeaconLabel { get; }
    public string Reason { get; }
    public ContractorDifficulty Difficulty { get; }
    public FixedPoint2 AlivePayout { get; }
    public FixedPoint2 DeadPayout { get; }

    public ContractorActiveContractData(
        string id,
        string targetName,
        string job,
        string beaconLabel,
        string reason,
        ContractorDifficulty difficulty,
        FixedPoint2 alivePayout,
        FixedPoint2 deadPayout)
    {
        Id = id;
        TargetName = targetName;
        Job = job;
        BeaconLabel = beaconLabel;
        Reason = reason;
        Difficulty = difficulty;
        AlivePayout = alivePayout;
        DeadPayout = deadPayout;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorRequisitionData
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public FixedPoint2 Cost { get; }
    public bool Affordable { get; }

    public ContractorRequisitionData(string id, string name, string description, FixedPoint2 cost, bool affordable)
    {
        Id = id;
        Name = name;
        Description = description;
        Cost = cost;
        Affordable = affordable;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorUplinkBoundUserInterfaceState : BoundUserInterfaceState
{
    public FixedPoint2 Reputation { get; }
    public string StatusText { get; }
    public ContractorContractOfferData[] Offers { get; }
    public bool HasActiveContract { get; }
    public ContractorActiveContractData ActiveContract { get; }
    public ContractorContractStage ActiveStage { get; }
    public ContractorRequisitionData[] Requisitions { get; }
    public bool CanOpenPortal { get; }

    public ContractorUplinkBoundUserInterfaceState(
        FixedPoint2 reputation,
        string statusText,
        ContractorContractOfferData[] offers,
        bool hasActiveContract,
        ContractorActiveContractData activeContract,
        ContractorContractStage activeStage,
        ContractorRequisitionData[] requisitions,
        bool canOpenPortal)
    {
        Reputation = reputation;
        StatusText = statusText;
        Offers = offers;
        HasActiveContract = hasActiveContract;
        ActiveContract = activeContract;
        ActiveStage = activeStage;
        Requisitions = requisitions;
        CanOpenPortal = canOpenPortal;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorAcceptContractMessage : BoundUserInterfaceMessage
{
    public string ContractId { get; }
    public ContractorDifficulty Difficulty { get; }

    public ContractorAcceptContractMessage(string contractId, ContractorDifficulty difficulty)
    {
        ContractId = contractId;
        Difficulty = difficulty;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorDeclineContractMessage : BoundUserInterfaceMessage
{
    public string ContractId { get; }

    public ContractorDeclineContractMessage(string contractId)
    {
        ContractId = contractId;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorBuyRequisitionMessage : BoundUserInterfaceMessage
{
    public string RequisitionId { get; }

    public ContractorBuyRequisitionMessage(string requisitionId)
    {
        RequisitionId = requisitionId;
    }
}

[Serializable, NetSerializable]
public sealed class ContractorOpenPortalMessage : BoundUserInterfaceMessage;