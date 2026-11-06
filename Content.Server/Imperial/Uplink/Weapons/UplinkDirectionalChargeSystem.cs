using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Imperial.Uplink.Weapons;
using Content.Shared.Sticky;
using Content.Shared.Trigger;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Uplink.Weapons;

/// <summary>
/// X-4 из TG (grenade/c4/x4): directional = TRUE, explosion_arc = 120.
/// </summary>
public sealed class UplinkDirectionalChargeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> Structural = "Structural";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UplinkDirectionalChargeComponent, EntityStuckEvent>(OnStuck);
        SubscribeLocalEvent<UplinkDirectionalChargeComponent, TriggerEvent>(OnTrigger);
    }

    /// <summary>aim_dir = get_dir(user, target).</summary>
    private void OnStuck(Entity<UplinkDirectionalChargeComponent> ent, ref EntityStuckEvent args)
    {
        ent.Comp.Target = args.Target;
        var dir = _transform.GetWorldPosition(args.Target) - _transform.GetWorldPosition(args.User);
        ent.Comp.Direction = dir.LengthSquared() > 0.0001f ? Vector2.Normalize(dir) : Vector2.UnitY;
    }

    private void OnTrigger(Entity<UplinkDirectionalChargeComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != ent.Comp.KeyIn)
            return;

        args.Handled = true;

        if (ent.Comp.Target is { } target && !TerminatingOrDeleted(target) && IsDense(target))
        {
            var pos = _transform.GetMapCoordinates(target);
            var damage = new DamageSpecifier();
            damage.DamageDict[Structural] = ent.Comp.TargetStructuralDamage;
            _damageable.TryChangeDamage(target, damage, true, origin: args.User);

            var epicenter = pos.Offset(ent.Comp.Direction * ent.Comp.Offset);
            _explosion.QueueExplosion(epicenter,
                "Default",
                ent.Comp.TotalIntensity,
                ent.Comp.IntensitySlope,
                ent.Comp.MaxIntensity,
                ent,
                canCreateVacuum: true,
                addLog: true);
            QueueDel(ent);
            return;
        }

        _explosion.TriggerExplosive(ent, user: args.User);
    }

    private bool IsDense(EntityUid uid)
    {
        return TryComp<PhysicsComponent>(uid, out var physics) && physics.Hard && physics.BodyType == Robust.Shared.Physics.BodyType.Static;
    }
}
