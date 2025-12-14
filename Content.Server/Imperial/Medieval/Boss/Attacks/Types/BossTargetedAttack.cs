namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossTargetedAttack : BossAttack
{
    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        return new[] { boss };
    }
}
