using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.Uplink.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Store;

namespace Content.Server.Imperial.Uplink.Conditions;

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
