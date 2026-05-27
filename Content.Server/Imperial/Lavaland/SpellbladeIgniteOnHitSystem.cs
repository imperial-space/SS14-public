using Content.Shared.Imperial.Lavaland;
using Content.Shared.Projectiles;

namespace Content.Server.Imperial.Lavaland;

public sealed class SpellbladeIgniteOnHitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpellbladeIgniteOnHitComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnProjectileHit(EntityUid uid, SpellbladeIgniteOnHitComponent comp, ref ProjectileHitEvent args)
    {
        if (TerminatingOrDeleted(args.Target))
            return;

        Spawn(comp.FireTilePrototype, Transform(args.Target).Coordinates);
    }
}