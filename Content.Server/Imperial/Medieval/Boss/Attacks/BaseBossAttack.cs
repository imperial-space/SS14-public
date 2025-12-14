using System.Linq;

namespace Content.Server.Imperial.Medieval.Boss;

[ImplicitDataDefinitionForInheritors]
public abstract partial class BossAttack
{
    [DataField]
    public List<BossAttackAction> Actions = new();

    [DataField]
    public float Cooldown = 0f;

    [DataField]
    public float Priority = 1f;

    public TimeSpan NextAttack = TimeSpan.Zero;

    public bool Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var pickedTargets = PickTargets(boss, targets, entMan).Where(x => x.IsValid());
        if (!pickedTargets.Any())
            return false;

        foreach (var action in Actions)
            action.Execute(boss, pickedTargets, entMan);

        return true;
    }

    public abstract IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan);
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class BossAttackAction
{
    public abstract void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan);
}
