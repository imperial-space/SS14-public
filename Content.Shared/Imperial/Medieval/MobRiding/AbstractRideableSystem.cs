using System.Diagnostics.CodeAnalysis;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Content.Shared.Buckle;
using Content.Shared.Popups;
using Robust.Shared.Network;


namespace Content.Shared.Imperial.Medieval.MobRiding
{
    public abstract partial class AbstractRideableSystem : EntitySystem
    {

        #region Dependencies

        [Dependency] private readonly SharedBuckleSystem _buckle = default!;
        [Dependency] private readonly ThrowingSystem _throwing = default!;
        [Dependency] private readonly SharedStunSystem _stun = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly INetManager _netManager = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        #endregion

        #region Initialize

        public override void Initialize()
        {
            base.Initialize();
        }

        #endregion

        #region Other functions

        protected bool TryGetRideable(EntityUid rider,
            [NotNullWhen(true)] out EntityUid? entity,
            [NotNullWhen(true)] out RideableComponent? rideable,
            [NotNullWhen(true)] out RideableSprintComponent? sprint)
        {
            rideable = null;
            sprint = null;
            entity = null;

            if (!TryComp<BuckleComponent>(rider, out var buckle) || !buckle.BuckledTo.HasValue)
                return false;

            entity = buckle.BuckledTo.Value;
            return TryComp(buckle.BuckledTo.Value, out rideable) && TryComp(buckle.BuckledTo.Value, out sprint);
        }

        protected bool TryGetRideable(EntityUid rider,
            [NotNullWhen(true)] out EntityUid? entity,
            [NotNullWhen(true)] out RideableComponent? rideable) =>
            TryGetRideable(rider, out entity, out rideable, out _);

        protected bool TryGetSprint(EntityUid rider,
            [NotNullWhen(true)] out EntityUid? entity,
            [NotNullWhen(true)] out RideableSprintComponent? sprint) =>
            TryGetRideable(rider, out entity, out _, out sprint);

        protected void ThrowFromRideable(EntityUid uid,
            uint stunSeconds = 2,
            DamageSpecifier? damage = null,
            float throwingDistance = 0)
        {
            if (!TryComp<BuckleComponent>(uid, out var buckle))
                return;

            if(!buckle.BuckledTo.HasValue)
                return;
            var rideable = buckle.BuckledTo;


            _buckle.TryUnbuckle(uid, uid);

            if (damage != null)
                _damageable.TryChangeDamage(uid, damage);
            if (stunSeconds > 0)
            {
                _stun.TryAddStunDuration(uid, TimeSpan.FromSeconds(stunSeconds));
                _stun.TryKnockdown(uid, TimeSpan.FromSeconds(stunSeconds), true);
            }

            if (throwingDistance <= 0)
                return;
            if (!TryComp<PhysicsComponent>(rideable, out var physics))
                return;

            var direction = physics.LinearVelocity;
            _throwing.TryThrow(uid, direction * throwingDistance);
            _popup.PopupClient("Вы теряете равновесие и падаете с лошади.", uid, uid);
            if (!_netManager.IsServer)
                return;
            _audio.PlayPvs("/Audio/Imperial/Medieval/animal_horse.ogg", rideable.Value);
        }

        protected bool TryGetSkill(EntityUid uid, string skill, out int level)
        {
            level = 0;

            if (!TryComp<SkillsComponent>(uid, out var skills))
                return false;

            if (!skills.Levels.TryGetValue(skill, out level))
                return false;

            return true;
        }

        #endregion
    }
}
