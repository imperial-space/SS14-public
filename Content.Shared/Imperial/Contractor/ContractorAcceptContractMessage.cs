using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

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
