using System.Threading;
using Content.Shared.Imperial.Medieval.Boss;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class BossAppearance : BossAttackAction
{
    [DataField(required: true)]
    public string State;

    [DataField]
    public float Duration = 5f;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var appearance = entMan.System<AppearanceSystem>();
        appearance.SetData(boss, AdditionalBossVisuals.State, State);

        if (Duration > 0f)
        {
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(Duration), () =>
            {
                if (entMan.EntityExists(boss))
                {
                    appearance.SetData(boss, AdditionalBossVisuals.State, "none");
                }
            });
        }
    }
}
