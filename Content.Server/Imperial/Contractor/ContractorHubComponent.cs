using Content.Shared.Imperial.Contractor;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Contractor;

/// <summary>datum/contractor_hub: висит на разуме предателя, вошедшего в аплинк.</summary>
[RegisterComponent]
public sealed partial class ContractorHubComponent : Component
{
    [ViewVariables]
    public List<SyndicateContract> Contracts = new();

    [ViewVariables]
    public SyndicateContract? CurrentContract;

    [ViewVariables]
    public HashSet<EntityUid> AssignedTargets = new();

    [ViewVariables]
    public int ContractsCompleted;

    [ViewVariables]
    public int TcPayedOut;

    [ViewVariables]
    public int TcToRedeem;
}
