using Content.Server.Explosion.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Mining.Components;
using Content.Shared.Projectiles;
using Content.Shared.Trigger;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Lavaland.GibtoniteRock;

public sealed class GibtoniteRockSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GibtoniteRockComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<GibtoniteRockComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<GibtoniteRockComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<GibtoniteDefuserProjectileComponent, ProjectileHitEvent>(OnDefuserProjectileHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GibtoniteRockComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive)
                continue;

            comp.Timer -= frameTime;
            if (comp.Timer <= 0f)
            {
                Explode(uid);
            }
        }
    }

    private void OnAttacked(EntityUid uid, GibtoniteRockComponent comp, AttackedEvent args)
    {
        if (IsGibtoniteOre(args.Used))
        {
            Explode(uid);
            return;
        }

        if (!comp.IsActive && !comp.IsDefused)
            Activate(uid, comp);
    }

    private void OnInteractUsing(EntityUid uid, GibtoniteRockComponent comp, InteractUsingEvent args)
    {
        if (HasComp<MiningScannerComponent>(args.Used))
        {
            if (comp.IsActive)
            {
                Defuse(uid, comp);
                args.Handled = true;
            }
            return;
        }

        if (IsGibtoniteOre(args.Used) && !comp.IsActive && !comp.IsDefused)
        {
            Activate(uid, comp);
            args.Handled = true;
        }
    }

    private void OnDestruction(EntityUid uid, GibtoniteRockComponent comp, DestructionEventArgs args)
    {
        var coords = _transform.GetMapCoordinates(uid);

        if (comp.IsDefused)
        {
            Spawn("OreGibtonite", coords);
            return;
        }

        if (comp.IsActive)
        {
            _explosions.QueueExplosion(coords, "DemolitionCharge", totalIntensity: 300f, slope: 2.5f, maxTileIntensity: 10f, cause: uid);
        }
    }

    private void Activate(EntityUid uid, GibtoniteRockComponent comp)
    {
        comp.IsActive = true;
        comp.Timer = comp.ActivationTime;
        _appearance.SetData(uid, TriggerVisuals.VisualState, TriggerVisualState.Primed);
    }

    private void Defuse(EntityUid uid, GibtoniteRockComponent comp)
    {
        comp.IsActive = false;
        comp.IsDefused = true;
        _appearance.SetData(uid, TriggerVisuals.VisualState, TriggerVisualState.Unprimed);
    }

    private void Explode(EntityUid uid)
    {
        if (Deleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;

        var coords = _transform.GetMapCoordinates(uid);
        _explosions.QueueExplosion(coords, "DemolitionCharge", totalIntensity: 300f, slope: 2.5f, maxTileIntensity: 10f, cause: uid);
        QueueDel(uid);
    }

    private void OnDefuserProjectileHit(Entity<GibtoniteDefuserProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        if (!TryComp<GibtoniteRockComponent>(args.Target, out var rock))
            return;

        if (rock.IsActive && !rock.IsDefused)
            Defuse(args.Target, rock);
    }

    private bool IsGibtoniteOre(EntityUid uid) =>
        MetaData(uid).EntityPrototype?.ID == "OreGibtonite";
}
