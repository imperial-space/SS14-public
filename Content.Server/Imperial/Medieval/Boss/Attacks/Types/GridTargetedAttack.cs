using System.Linq;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class GridTargetedAttack : BossAttack
{
    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var xform = entMan.GetComponent<TransformComponent>(boss);
        if (!xform.GridUid.HasValue)
            return Enumerable.Empty<EntityUid>();

        return new[] { xform.GridUid.Value };
    }
}
