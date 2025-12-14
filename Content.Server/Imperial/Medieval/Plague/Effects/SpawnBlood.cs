using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class SpawnBlood : BasePlagueEffect
{
    public override SpawnBlood CreateInstance()
    {
        return new SpawnBlood()
        {
            Delay = this.Delay,
            Other = this.Other
        };
    }

    protected override void Effect(EntityUid uid, IEntityManager entMan)
    {
        var puddle = entMan.System<PuddleSystem>();
        var xform = entMan.GetComponent<TransformComponent>(uid);
        if (!entMan.TryGetComponent<BloodstreamComponent>(uid, out var blood))
            return;

        Solution sol = new(blood.BloodReagent, 20f);
        puddle.TrySpillAt(xform.Coordinates, sol, out _, false);
    }
}
