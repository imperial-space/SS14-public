using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Фазовые пули TG: не сталкиваются ни с чем, кроме живых существ.
/// </summary>
public sealed class UplinkPhasingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UplinkProjectileComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<UplinkProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Phasing && !HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
