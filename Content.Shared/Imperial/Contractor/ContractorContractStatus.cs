using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

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
