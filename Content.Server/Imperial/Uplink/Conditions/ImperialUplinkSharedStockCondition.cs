using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Uplink.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Store;

namespace Content.Server.Imperial.Uplink.Conditions;

/// <summary>
/// Аналог stock_key из TG: общий лимит покупок на несколько позиций с одинаковым ключом.
/// </summary>
public sealed partial class ImperialUplinkSharedStockCondition : ListingCondition
{
    [DataField(required: true)]
    public string Key = string.Empty;

    [DataField]
    public int Stock = 1;

    public override bool Condition(ListingConditionArgs args)
    {
        return args.EntityManager.System<ImperialUplinkSystem>().GetSharedStockPurchases(args.StoreEntity, Key) < Stock;
    }
}
