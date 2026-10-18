namespace Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;

/// <summary>
/// obj/item/mining_voucher: применяется на шахтёрский автомат, даёт выбрать один набор.
/// </summary>
[RegisterComponent]
public sealed partial class MiningVoucherComponent : Component
{
    /// <summary>Автомат, на который применили ваучер; набор выпадает у него.</summary>
    [ViewVariables]
    public EntityUid? Redeemer;
}
