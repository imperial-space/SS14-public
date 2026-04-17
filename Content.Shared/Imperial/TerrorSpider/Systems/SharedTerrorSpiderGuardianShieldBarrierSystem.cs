using Content.Shared.Imperial.TerrorSpider.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Imperial.TerrorSpider.Systems;

public sealed class SharedTerrorSpiderGuardianShieldBarrierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderGuardianShieldBarrierComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<TerrorSpiderGuardianShieldBarrierComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        if (HasComp<TerrorSpiderWebBuffReceiverComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
