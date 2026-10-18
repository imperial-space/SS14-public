using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Uplink.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Store;

namespace Content.Server.Imperial.Uplink.Conditions;

/// <summary>
/// Аналог purchasable_from из TG: кому доступна позиция — предателям и/или ядерным оперативникам.
/// Ядерщики определяются по тегу NukeOpsUplink на сторе; стор без сущности (ящик излишков) считается предательским.
/// </summary>
public sealed partial class ImperialUplinkPurchasableCondition : ListingCondition
{
    [DataField]
    public bool Traitor;

    [DataField]
    public bool NukeOps;

    public override bool Condition(ListingConditionArgs args)
    {
        var nuke = args.EntityManager.System<ImperialUplinkSystem>().IsNukeOpsStore(args.StoreEntity);
        return nuke ? NukeOps : Traitor;
    }
}

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

/// <summary>
/// Аналог population_minimum из TG: число вошедших в раунд игроков.
/// </summary>
public sealed partial class ImperialUplinkPopulationCondition : ListingCondition
{
    [DataField(required: true)]
    public int Players;

    public override bool Condition(ListingConditionArgs args)
    {
        var ticker = args.EntityManager.System<GameTicker>();
        return ticker.PlayerGameStatuses.Values.Count(x => x == PlayerGameStatus.JoinedGame) >= Players;
    }
}

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
