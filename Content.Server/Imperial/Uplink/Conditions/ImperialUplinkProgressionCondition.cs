using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Uplink.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Store;

namespace Content.Server.Imperial.Uplink.Conditions;

/// <summary>
/// Аналог progression_minimum из TG: репутация предателя растёт на одно очко в минуту раунда.
/// У ядерщиков прогрессии нет.
/// </summary>
public sealed partial class ImperialUplinkProgressionCondition : ListingCondition
{
    [DataField(required: true)]
    public int Minutes;

    public override bool Condition(ListingConditionArgs args)
    {
        if (args.EntityManager.System<ImperialUplinkSystem>().IsNukeOpsStore(args.StoreEntity))
            return true;

        return args.EntityManager.System<GameTicker>().RoundDuration() >= TimeSpan.FromMinutes(Minutes);
    }
}
