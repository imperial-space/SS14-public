using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;

/// <summary>datum/voucher_set/mining: наборы, которые выдаёт шахтёрский ваучер.</summary>
public static class MiningVoucherKits
{
    public sealed record Kit(LocId Name, LocId Description, EntProtoId Icon, EntProtoId[] Items);

    public static readonly Kit[] All =
    {
        // crusher_kit
        new("mining-voucher-kit-crusher-name", "mining-voucher-kit-crusher-desc", "WeaponKineticCrusher",
            new EntProtoId[] { "WeaponKineticCrusher", "FireExtinguisherMini", "ExtendedEmergencyOxygenTankFilled" }),
        // extraction_kit
        new("mining-voucher-kit-extraction-name", "mining-voucher-kit-extraction-desc", "Fulton",
            new EntProtoId[] { "Fulton", "FultonBeacon", "MarkerBeacon30" }),
        // resonator_kit
        new("mining-voucher-kit-resonator-name", "mining-voucher-kit-resonator-desc", "WeaponResonator",
            new EntProtoId[] { "WeaponResonator", "FireExtinguisherMini" }),
        // survival_capsule
        new("mining-voucher-kit-survival-name", "mining-voucher-kit-survival-desc", "ClothingBeltMiningWebbing",
            new EntProtoId[] { "ClothingBeltMiningWebbingFilled" }),
        // conscription_kit
        new("mining-voucher-kit-conscription-name", "mining-voucher-kit-conscription-desc", "ImperialMiningConscriptionKit",
            new EntProtoId[] { "ImperialMiningConscriptionKit" }),
    };
}
