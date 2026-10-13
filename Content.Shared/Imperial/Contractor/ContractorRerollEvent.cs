namespace Content.Shared.Imperial.Contractor;

/// <summary>
/// uplink_item/contractor/reroll: покупка в аплинке перегенерирует цели неактивных контрактов.
/// </summary>
[DataDefinition]
public sealed partial class ContractorRerollEvent : EntityEventArgs;
