using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Imperial.Medieval.MobRiding;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.MobRiding
{
    public sealed partial class RideableSystem : AbstractRideableSystem
    {
        [Dependency] private readonly NPCSystem _npc = default!;
        [Dependency] private readonly SharedBuckleSystem _buckle = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly FixtureSystem _fixtureSystem = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedWieldableSystem _wieldable = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        #region Const
        private readonly string[] _clashOne = new[] {
            "/Audio/Imperial/Medieval/clash_one1.ogg",
            "/Audio/Imperial/Medieval/clash_one2.ogg",
        };

        private readonly string _clashBoth = "/Audio/Imperial/Medieval/clash_both.ogg";
        private readonly string _clashNone = "/Audio/Imperial/Medieval/wood_parry.ogg";

        #endregion

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RideableComponent, StartRideEvent>(OnBuckled);
            SubscribeLocalEvent<RideableComponent, StopRideEvent>(OnUnbuckled);

            SubscribeLocalEvent<RideableComponent, BeforeBuckledEvent>(OnSelfBuckled);
            SubscribeLocalEvent<RideableComponent, UnbuckledEvent>(OnSelfUnbuckled);

            SubscribeLocalEvent<RideableComponent, StrapAttemptEvent>(OnTryRide);

            SubscribeLocalEvent<RideableComponent, MobStateChangedEvent>(OnMobStateChanged);

            SubscribeLocalEvent<BuckleComponent, TryUnbuckleEvent>(OnTryUnbuckle);

            SubscribeLocalEvent<PikeComponent, ItemWieldedEvent>(OnWielded);
            SubscribeLocalEvent<PikeComponent, ItemUnwieldedEvent>(OnUnwielded);

            SubscribeLocalEvent<RideableComponent, StartCollideEvent>(HandleCollide);

            SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);

            SubscribeLocalEvent<BuckleComponent, PullAttemptEvent>(OnTryPullRider);
            SubscribeLocalEvent<BuckleComponent, UnbuckleAttemptEvent>(OnUnbuckleAttempt);
        }

        #region Pikes

        private void HandleCollide(EntityUid uid, RideableComponent comp, ref StartCollideEvent args)
        {
            if (args.OurFixtureId != comp.PikeShapeId)
                return;
            if (args.OtherEntity == comp.Rider)
                return;
            if (!TryComp<MobStateComponent>(args.OtherEntity, out var mobState))
                return;
            if (mobState.CurrentState != MobState.Alive)
                return;
            if (!comp.Rider.HasValue)
                return;
            if (!TryGetPike(comp.Rider.Value, out var item, out var pikeComp))
                return;
            if (TryComp<UseDelayComponent>(item, out var useDelay) && _useDelay.IsDelayed((item.Value, useDelay)))
                return;
            if(args.OtherFixtureId != comp.PikeShapeId)
                PikeHitEntity(uid, comp, args.OtherEntity, comp.Rider.Value, pikeComp);
            else
                PikeClash(uid, comp, args.OtherEntity);
        }

        private void PikeHitEntity(EntityUid uid, RideableComponent comp, EntityUid other, EntityUid rider, PikeComponent pikeComp, uint stunSeconds = 2, bool throwOther = true)
        {
            if (!TryComp<PhysicsComponent>(uid, out var rideablePhysics))
                return;

            if (comp.StunList.TryGetValue(other, out var time))
            {
                var diff = _gameTiming.CurTime - time;
                if (diff.Duration() < TimeSpan.FromSeconds(5))
                    return;
            }

            if (TryComp<RideableComponent>(other, out var otherRideable))
            {
                if (otherRideable.IsRiding && otherRideable.Rider.HasValue)
                {
                    ThrowFromRideable(otherRideable.Rider.Value, damage: pikeComp.RidingDamage, throwingDistance: 0.6f);
                }
            }

            var direction = rideablePhysics.LinearVelocity;

            if(throwOther)
                _throwing.TryThrow(other, direction * 0.6f);
            if (stunSeconds > 0)
            {
                _stun.TryAddStunDuration(other, TimeSpan.FromSeconds(stunSeconds));
                _stun.TryKnockdown(other, TimeSpan.FromSeconds(stunSeconds), true);
            }

            var velocity = rideablePhysics.LinearVelocity.Length();

            _popup.PopupEntity("Вы сшибаете пехотинца с дороги.", rider, rider);

            _damageable.TryChangeDamage(other, pikeComp.RidingDamage * velocity);
            var oneSound = _random.Pick(_clashOne);
            _audio.PlayPvs(oneSound, other);

            DelayPike(rider);
            if (!comp.StunList.TryAdd(other, _gameTiming.CurTime))
                comp.StunList[other] = _gameTiming.CurTime;

            comp.Dirty();
        }

        private void DelayPike(EntityUid rider, EntityUid? item = null)
        {
            if(item == null)
            {
                if (!TryGetPike(rider, out item, out _))
                    return;
            }

            if (TryComp<UseDelayComponent>(item, out var useDelay) && _useDelay.IsDelayed((item.Value, useDelay)))
                return;

            if (TryComp<WieldableComponent>(item.Value, out var wieldable))
                _wieldable.TryUnwield(item.Value, wieldable, rider);

            if (useDelay != null)
                _useDelay.TryResetDelay((item.Value, useDelay));
        }

        private bool TryGetPike(EntityUid uid, [NotNullWhen(true)] out EntityUid? pike, [NotNullWhen(true)] out PikeComponent? comp)
        {
            pike = null;
            comp = null;

            pike = _hands.GetActiveItem(uid);
            return pike.HasValue && TryComp(pike, out comp);
        }

        private sealed class Skills
        {
            public readonly int Intelligence;
            public readonly int Endurance;
            public readonly int Agility;
            public readonly int Strength;
            public readonly int Vitality;

            public Skills(RideableSystem system, EntityUid uid)
            {
                system.TryGetSkill(uid, "Intelligence", out Intelligence);
                system.TryGetSkill(uid, "Endurance", out Endurance);
                system.TryGetSkill(uid, "Agility", out Agility);
                system.TryGetSkill(uid, "Strength", out Strength);
                system.TryGetSkill(uid, "Vitality", out Vitality);
            }
        }

        private bool TryGetClashData(EntityUid uid,
            [NotNullWhen(true)] out RideableComponent? comp,
            [NotNullWhen(true)] out EntityUid? rider,
            out float attackStrength,
            out float stability,
            out float finalAttack,
            out float finalStability)
        {
            comp = null;
            rider = null;
            attackStrength = 0;
            stability = 0;
            finalAttack = 0;
            finalStability = 0;


            if (!TryComp(uid, out comp))
                return false;

            if (!comp.Rider.HasValue)
                return false;

            rider = comp.Rider;

            if (!HasComp<SkillsComponent>(rider))
                return false;

            var skills = new Skills(this, rider.Value);

            attackStrength = (skills.Strength * 1.4f) + (skills.Agility * 1.0f) + (skills.Endurance * 0.6f);
            stability = (skills.Vitality * 1.6f) + (skills.Agility * 1.15f) + (skills.Strength * 0.3f);

            finalAttack = attackStrength + _random.Next(1, 21);
            finalStability = stability + _random.Next(1, 21);

            return true;
        }

        private CheckResult ResolveHitOutcome(float finalAttack, float otherFinalAttack)
        {
            var difference = finalAttack - otherFinalAttack;

            return difference switch
            {
                > 10 => CheckResult.Self,
                < -10 => CheckResult.Other,
                _ => CheckResult.Draw
            };
        }

        private CheckResult ResolveStabilityOutcome(float finalStability,
            float otherFinalStability,
            float stabilityDifficulty)
        {
            var selfPasses = finalStability >= stabilityDifficulty;
            var otherPasses = otherFinalStability >= stabilityDifficulty;

            return (selfPasses, otherPasses) switch
            {
                (true, true) => CheckResult.Both,
                (false, false) => CheckResult.Draw,
                (true, false) => CheckResult.Self,
                (false, true) => CheckResult.Other
            };
        }

        private void ThrowClash(EntityUid rideable, EntityUid rider, EntityUid otherRider)
        {
            if (!TryComp<PhysicsComponent>(rideable, out var rideablePhysics))
                return;
            var velocity = rideablePhysics.LinearVelocity.Length();
            if (!TryGetPike(rider, out var pike, out var pikeComp))
                return;
            ThrowFromRideable(otherRider, 2, pikeComp.RidingDamage * velocity, throwingDistance: 0.6f);
        }

        private void ThrowClashBoth(EntityUid rideable, EntityUid otherRideable, EntityUid rider, EntityUid otherRider)
        {
            if (!TryComp<PhysicsComponent>(rideable, out var rideablePhysics))
                return;
            if (!TryComp<PhysicsComponent>(otherRideable, out var otherRideablePhysics))
                return;

            var velocity = rideablePhysics.LinearVelocity.Length();
            var otherVelocity = otherRideablePhysics.LinearVelocity.Length();

            if (!TryGetPike(rider, out var pike, out var pikeComp))
                return;
            if (!TryGetPike(otherRider, out var otherPike, out var otherPikeComp))
                return;

            ThrowFromRideable(otherRider, 2, pikeComp.RidingDamage * velocity, throwingDistance: 0.6f);
            ThrowFromRideable(rider, 2, otherPikeComp.RidingDamage * otherVelocity, throwingDistance: 0.6f);
        }

        private void PikeClash(EntityUid uid, RideableComponent comp, EntityUid other)
        {
            if (uid.Id < other.Id)
                return;

            if (!TryGetClashData(uid,
                    out _,
                    out var rider,
                    out var attackStrength,
                    out var stability,
                    out var finalAttack,
                    out var finalStability))
                return;

            if (!TryGetClashData(other,
                    out var otherComp,
                    out var otherRider,
                    out var otherAttackStrength,
                    out var otherStability,
                    out var otherFinalAttack,
                    out var otherFinalStability))
                return;

            var hitResult = ResolveHitOutcome(finalAttack, otherFinalAttack);

            switch (hitResult)
            {
                case CheckResult.Self:
                    ThrowClash(uid, rider.Value, otherRider.Value);
                    break;
                case CheckResult.Other:
                    ThrowClash(other, otherRider.Value, rider.Value);
                    break;
                case CheckResult.Draw:
                    var stabilityDifficulty = (finalAttack + otherFinalAttack) / 2;
                    var stabilityResult =
                        ResolveStabilityOutcome(finalStability, otherFinalStability, stabilityDifficulty);

                    var oneSound = _random.Pick(_clashOne);
                    switch (stabilityResult)
                    {
                        // TODO: текст в Loc, посмотреть, почему не работают звуки
                        // TODO: сделаю после ОБТ, сейчас впадлу
                        case CheckResult.Self:
                            _popup.PopupEntity("Противник не удерживается в седле и падает.", rider.Value, rider.Value);
                            _audio.PlayPvs(oneSound, uid);
                            ThrowClash(uid, rider.Value, otherRider.Value);
                            DelayPike(rider.Value);
                            break;
                        case CheckResult.Other:
                            _popup.PopupEntity("Противник не удерживается в седле и падает.", otherRider.Value, otherRider.Value);
                            _audio.PlayPvs(oneSound, other);
                            ThrowClash(other, otherRider.Value, rider.Value);
                            DelayPike(otherRider.Value);
                            break;
                        case CheckResult.Draw:
                            _audio.PlayPvs(_clashBoth, uid);
                            ThrowClashBoth(uid, other, rider.Value, otherRider.Value);
                            break;
                        case CheckResult.Both:
                            _popup.PopupEntity("Вы сталкиваетесь, но оба удерживаетесь в седле.", rider.Value, rider.Value);
                            _popup.PopupEntity("Вы сталкиваетесь, но оба удерживаетесь в седле.", otherRider.Value, otherRider.Value);
                            DelayPike(rider.Value);
                            DelayPike(otherRider.Value);
                            _audio.PlayPvs(_clashNone, uid);
                            break;
                    }
                    Logger.Debug($"Clash Draw: stb: {finalStability} vs {otherFinalStability}, result: {stabilityResult}");
                    break;

            }
            Logger.Debug($"Clash: {rider} vs {otherRider}, atk: {finalAttack} vs {otherFinalAttack}, result: {hitResult}");
        }

        private bool CheckPike(EntityUid uid, out EntityUid? item)
        {
            item = _hands.GetActiveItem(uid);

            if (!item.HasValue)
                return false;

            if (!HasComp<PikeComponent>(item.Value))
                return false;

            if (!TryComp<WieldableComponent>(item.Value, out var wieldable))
                return false;

            return wieldable.Wielded;
        }

        private void OnWielded(EntityUid uid, PikeComponent comp, ref ItemWieldedEvent args)
        {
            var rider = args.User;

            if (!TryGetRideable(rider, out var rideableEntity, out var rideable))
                return;

            if (rideable.Pike != null)
                return;

            rideable.Pike = uid;
            rideable.Dirty();

            CreatePikeFixture(rideableEntity.Value, rideable);
        }

        private void OnUnwielded(EntityUid uid, PikeComponent comp, ref ItemUnwieldedEvent args)
        {
            var rider = args.User;

            if (!TryGetRideable(rider, out var rideableEntity, out var rideable))
                return;

            if (rideable.Pike != uid)
                return;

            rideable.Pike = null;
            rideable.Dirty();

            RemovePikeFixture(rideableEntity.Value, rideable);
        }

        private void CreatePikeFixture(EntityUid uid, RideableComponent rideable)
        {
            _fixtureSystem.TryCreateFixture(uid, rideable.PikeShape, rideable.PikeShapeId, density: 1, collisionLayer: (int)CollisionGroup.PikeLayer, collisionMask: (int)CollisionGroup.PikeMask, hard:false);
        }

        private void RemovePikeFixture(EntityUid uid, RideableComponent rideable)
        {
            _fixtureSystem.DestroyFixture(uid, rideable.PikeShapeId);
        }

        #endregion

        private void OnTryPullRider(EntityUid uid, BuckleComponent comp, ref PullAttemptEvent args)
        {
            if (args.PulledUid != uid)
                return;

            if (!comp.Buckled)
                return;

            if (!comp.BuckledTo.HasValue)
                return;

            var buckled = comp.BuckledTo.Value;

            if (HasComp<RideableComponent>(buckled))
                args.Cancelled = true;
        }

        private void OnTryUnbuckle(EntityUid uid, BuckleComponent comp, ref TryUnbuckleEvent args)
        {
            _buckle.TryUnbuckle(args.Buckle, args.Buckle);
        }

        private void OnUnbuckleAttempt(EntityUid uid, BuckleComponent comp, ref UnbuckleAttemptEvent args)
        {
            if (args.User == args.Buckle.Owner)
                return;

            if (!comp.Buckled)
                return;

            if (!comp.BuckledTo.HasValue)
                return;

            var buckled = comp.BuckledTo.Value;

            if (HasComp<RideableComponent>(buckled))
                args.Cancelled = true;

            return;
        }

        private void OnTryRide(EntityUid uid, RideableComponent component, ref StrapAttemptEvent args)
        {
            if (!CheckAgility(args.Buckle))
            {
                _popup.PopupEntity(Loc.GetString("imperial-medieval-rideable-skill-popup"), args.Buckle.Owner);
                args.Cancelled = true;
            }
            else if (!CheckState(args.Strap))
            {
                args.Cancelled = true;
            }
        }

        private bool CheckState(EntityUid uid)
        {
            return _mobState.IsAlive(uid);
        }

        private bool CheckAgility(BuckleComponent buckle)
        {
            if (!TryComp<SkillsComponent>(buckle.Owner, out var skills))
                return false;

            if (!skills.Levels.TryGetValue("Agility", out var agility))
                return false;

            if (agility < 9)
                return false;

            return true;
        }

        private void OnSelfBuckled(EntityUid uid, RideableComponent component, ref BeforeBuckledEvent args)
        {
            if (args.Cancelled)
                return;

            if (component.IsRiding && component.Rider.HasValue)
            {
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider);
            }

            component.CanRide = false;
        }

        private void OnSelfUnbuckled(EntityUid uid, RideableComponent component, ref UnbuckledEvent args)
        {
            component.CanRide = true;
        }

        private void OnBuckled(EntityUid uid, RideableComponent component, ref StartRideEvent args)
        {
            if (!component.CanRide && component.Rider.HasValue)
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider.Value);

            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.SleepNPC(uid, htn);

            if (CheckPike(args.Buckle.Owner, out var pike))
            {
                CreatePikeFixture(uid, component);
                component.Pike = pike;
            }

            component.Dirty();
        }

        private void OnUnbuckled(EntityUid uid, RideableComponent component, ref StopRideEvent args)
        {
            if (TryComp<HTNComponent>(uid, out var htn))
                _npc.WakeNPC(uid, htn);

            RemovePikeFixture(uid, component);
            component.Pike = null;

            component.Dirty();

        }

        private void OnMobStateChanged(EntityUid uid, RideableComponent component, ref MobStateChangedEvent args)
        {
            if (args.NewMobState == MobState.Alive)
                return;

            if(component.Rider.HasValue)
                _buckle.TryUnbuckle(component.Rider.Value, component.Rider.Value);
        }

        private void OnEntityTerminating(ref EntityTerminatingEvent args)
        {
            var enumerator = EntityQueryEnumerator<RideableComponent>();

            while (enumerator.MoveNext(out var uid, out var rideable))
            {
                rideable.StunList.Remove(args.Entity.Owner);
            }
        }
    }

}
