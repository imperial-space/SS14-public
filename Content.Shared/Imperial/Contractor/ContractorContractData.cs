using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

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
