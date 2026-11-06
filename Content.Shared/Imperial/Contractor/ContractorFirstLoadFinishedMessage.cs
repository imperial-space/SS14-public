using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

/// <summary>PRG_set_first_load_finished.</summary>
[Serializable, NetSerializable]
public sealed class ContractorFirstLoadFinishedMessage : BoundUserInterfaceMessage;
