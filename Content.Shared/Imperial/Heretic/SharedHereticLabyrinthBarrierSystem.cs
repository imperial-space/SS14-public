using Content.Shared.Imperial.Heretic.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Imperial.Heretic;

public sealed class SharedHereticLabyrinthBarrierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticLabyrinthBarrierComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<HereticLabyrinthBarrierComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        if (HasComp<HereticComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
