using Content.Shared.Imperial.Contractor;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Contractor;

/// <summary>datum/syndicate_contract.</summary>
public sealed class SyndicateContract
{
    public int Id;
    public ContractorContractStatus Status = ContractorContractStatus.Inactive;
    public ContractorPayoutType PayoutType;

    /// <summary>contract.target: разум цели.</summary>
    public EntityUid? TargetMind;
    public string TargetName = string.Empty;
    public string TargetRank = string.Empty;

    /// <summary>contract.payout и contract.payout_bonus.</summary>
    public int Payout;
    public int PayoutBonus;

    /// <summary>contract.dropoff: навигационный маяк, обозначающий зону.</summary>
    public EntityUid? Dropoff;
    public string DropoffName = string.Empty;

    public int Ransom;
    public string WantedMessage = string.Empty;
}
