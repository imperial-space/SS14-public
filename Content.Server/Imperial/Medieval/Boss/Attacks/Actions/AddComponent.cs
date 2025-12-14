using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class AddComponent : BossAttackAction
{
    [DataField(required: true)]
    public ComponentRegistry Components;

    [DataField]
    public bool ToBoss = false;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        if (ToBoss)
        {
            entMan.AddComponents(boss, Components);
            return;
        }

        foreach (var target in targets)
        {
            entMan.AddComponents(target, Components);
        }
    }
}
