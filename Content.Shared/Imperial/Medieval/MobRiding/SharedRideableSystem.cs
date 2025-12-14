using System.Diagnostics.CodeAnalysis;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics;

namespace Content.Shared.Imperial.Medieval.MobRiding
{
    public sealed partial class SharedRideableSystem : EntitySystem
    {

        #region Dependencies
        [Dependency] private readonly SharedMoverController _mover = default!;
        #endregion

        #region Initialize
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RideableComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
            SubscribeLocalEvent<MeleeWeaponComponent, BeforeMeleeHitEvent>(OnBeforeMeleeHit);
            SubscribeLocalEvent<BuckleComponent, RayCastSort>(OnRayCastSort);
            SubscribeLocalEvent<ProjectileComponent, ProjectileBeforeHitEvent>(OnBeforeProjectileHit);

            SubscribeLocalEvent<RideableComponent, StrappedEvent>(OnBuckled);
            SubscribeLocalEvent<RideableComponent, UnstrappedEvent>(OnUnbuckled);

        }
        #endregion

        #region Other functions
        private bool TryGetRideable(EntityUid rider, [NotNullWhen(true)] out EntityUid? entity, [NotNullWhen(true)] out RideableComponent? rideable, [NotNullWhen(true)] out RideableSprintComponent? sprint)
        {
            rideable = null;
            sprint = null;
            entity = null;

            if (!TryComp<BuckleComponent>(rider, out var buckle) || !buckle.BuckledTo.HasValue)
                return false;

            entity = buckle.BuckledTo.Value;
            return TryComp(buckle.BuckledTo.Value, out rideable) && TryComp(buckle.BuckledTo.Value, out sprint);
        }

        private bool TryGetRideable(EntityUid rider, [NotNullWhen(true)] out EntityUid? entity, [NotNullWhen(true)] out RideableComponent? rideable) =>
            TryGetRideable(rider, out entity, out rideable, out _);

        private bool TryGetSprint(EntityUid rider, [NotNullWhen(true)] out EntityUid? entity, [NotNullWhen(true)] out RideableSprintComponent? sprint) =>
            TryGetRideable(rider, out entity, out _, out sprint);
        #endregion

        #region Damage ignore
        private void OnRayCastSort(EntityUid uid, BuckleComponent component, ref RayCastSort args)
        {
            var toRemove = new List<RayCastResults>();

            for (int i = 0; i < args.HitEntities.Count; i++)
            {
                var result = args.HitEntities[i];
                var target = result.HitEntity;
                if (!TryComp<RideableComponent>(target, out var rideable))
                    continue;

                if (!rideable.Rider.HasValue)
                    continue;

                if (rideable.Rider.Value == uid)
                {
                    args.HitEntities.RemoveAt(i);
                }
            }
        }

        private void OnBeforeProjectileHit(EntityUid uid,
            ProjectileComponent component,
            ref ProjectileBeforeHitEvent args)
        {
            if (!TryComp<RideableComponent>(args.Target, out var rideable)) return;
            if (!rideable.Rider.HasValue) return;
            if (!args.Shooter.HasValue) return;

            if (rideable.Rider.Value == args.Shooter.Value)
                args.Cancelled = true;

        }

        private void OnBeforeDamageChanged(EntityUid uid, RideableComponent component, ref BeforeDamageChangedEvent args)
        {
            if (!args.Origin.HasValue || !component.Rider.HasValue)
                return;
            if (args.Origin.Value == component.Rider.Value)
                args.Cancelled = true;
        }

        private void OnBeforeMeleeHit(EntityUid uid, MeleeWeaponComponent component, ref BeforeMeleeHitEvent args)
        {
            // по факту бесполезно из-за того, что всё удаляется ещё при рейкасте
            for (var i = args.HitEntities.Count - 1; i >= 0; i--)
            {
                var target = args.HitEntities[i];

                if (!TryComp<RideableComponent>(target, out var rideable)) continue;
                if (!rideable.Rider.HasValue) continue;

                if (rideable.Rider.Value == args.User)
                {
                    args.HitEntities.RemoveAt(i);
                }
            }
        }
        #endregion

        #region Buckle
        public void OnBuckled(EntityUid uid, RideableComponent component, ref StrappedEvent args)
        {
            var strap = args.Strap;
            var buckle = args.Buckle;

            component.Rider = buckle.Owner;
            component.IsRiding = true;
            _mover.SetRelay(buckle.Owner, uid);

            var ev = new StartRideEvent(strap, buckle);
            RaiseLocalEvent(uid, ref ev);
            component.Dirty();
        }

        public void OnUnbuckled(EntityUid uid, RideableComponent component, ref UnstrappedEvent args)
        {
            if (!component.Rider.HasValue)
                return;
            RemComp<RelayInputMoverComponent>(component.Rider.Value);

            component.Rider = null;
            component.IsRiding = false;
            var ev = new StopRideEvent(args.Strap, args.Buckle);
            RaiseLocalEvent(uid, ref ev);
            component.Dirty();
        }
        #endregion
    }

}

#region Events
[ByRefEvent]
public readonly record struct StartRideEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

[ByRefEvent]
public readonly record struct StopRideEvent(Entity<StrapComponent> Strap, Entity<BuckleComponent> Buckle);

[ByRefEvent]
public readonly record struct TryUnbuckleEvent(Entity<BuckleComponent> Buckle);
#endregion
