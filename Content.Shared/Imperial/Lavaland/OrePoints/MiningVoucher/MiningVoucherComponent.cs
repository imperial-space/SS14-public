using Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;

/// <summary>
/// Marks an item as a mining voucher. When used on a mining vending machine,
/// opens a kit selection UI, giving the player one kit of their choice.
/// </summary>
[RegisterComponent]
public sealed partial class MiningVoucherComponent : Component
{
}
