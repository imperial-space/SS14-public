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

/// <summary>CONTRACT_PAYOUT_*.</summary>
public enum ContractorPayoutType : byte
{
    Large = 1,
    Medium = 2,
    Small = 3,
}

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

/// <summary>Состояние программы контрактника на конкретном планшете.</summary>
[RegisterComponent]
public sealed partial class ContractorUplinkStateComponent : Component
{
    /// <summary>traitor_data: разум, вошедший в программу.</summary>
    [ViewVariables]
    public EntityUid? LoggedInMind;

    [ViewVariables]
    public bool FirstLoad = true;

    [ViewVariables]
    public bool InfoScreen = true;

    [ViewVariables]
    public string Error = string.Empty;
}

/// <summary>closet/supplypod/extractionpod.</summary>
[RegisterComponent]
public sealed partial class ContractorExtractionPodComponent : Component
{
    [ViewVariables]
    public EntityUid ContractorMind;

    [ViewVariables]
    public int ContractId;

    [ViewVariables]
    public bool Used;
}

/// <summary>Похищенный: эффекты, затем возврат на станцию.</summary>
[RegisterComponent]
public sealed partial class ContractorVictimComponent : Component
{
    [ViewVariables]
    public int Level;

    [ViewVariables]
    public TimeSpan NextExperience;

    [ViewVariables]
    public TimeSpan ReturnAt;

    /// <summary>Хранилище вещей жертвы.</summary>
    [ViewVariables]
    public EntityUid? Belongings;

    /// <summary>Маяк зоны отправки, куда жертву вернут.</summary>
    [ViewVariables]
    public EntityUid? Dropoff;

    [ViewVariables]
    public EntityCoordinates? FallbackReturn;
}
