using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

public sealed class SharedHereticCosmicFieldSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCosmicFieldBlockComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<HereticCosmicFieldBlockComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        // Пропускаем еретиков и немаркированных — блокируем только StarMark без HereticComponent
        if (HasComp<HereticComponent>(args.OtherEntity) || !HasComp<StarMarkComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
