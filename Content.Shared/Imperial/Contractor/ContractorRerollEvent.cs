using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Contractor;

/// <summary>
/// uplink_item/contractor/reroll: покупка в аплинке перегенерирует цели неактивных контрактов.
/// Хранится в листинге магазина и передаётся клиенту вместе с его состоянием.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class ContractorRerollEvent : EntityEventArgs;
