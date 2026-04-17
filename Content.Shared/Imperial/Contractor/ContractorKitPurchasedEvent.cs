using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Contractor;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ContractorKitPurchasedEvent;