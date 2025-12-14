using System.Linq;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class PlayerTargetedAttack : BossAttack
{
    [DataField]
    public (int, int) TargetLimits = (1, 3);

    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var players = targets.ToList();
        Random.Shared.Shuffle(players);

        return players.Take(Math.Min(random.Next(TargetLimits.Item1, TargetLimits.Item2), players.Count));
    }
}
