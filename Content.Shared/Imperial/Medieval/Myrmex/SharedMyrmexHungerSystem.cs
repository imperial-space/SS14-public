using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Examine;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Medieval.Myrmex
{
    public sealed partial class SharedMyrmexHungerSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _speedModifier = default!;
        [Dependency] private readonly AlertsSystem _alertsSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MyrmexHungerComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedRefresh);
            SubscribeLocalEvent<MyrmexHungerComponent, ComponentInit>(OnInit);

            SubscribeLocalEvent<MyrmexHungerComponent, StaminaModifyEvent>(OnModifyStaminaDamage);
            SubscribeLocalEvent<MyrmexHungerComponent, GetMeleeDamageEvent>(OnGetDamage);
            SubscribeLocalEvent<MyrmexHungerComponent, DamageModifyEvent>(OnGetDamageModifiers);
            SubscribeLocalEvent<MyrmexHungerComponent, ExaminedEvent>(OnExamined);
        }

        private void OnInit(EntityUid uid, MyrmexHungerComponent comp, ref ComponentInit args)
        {
            comp.LastEaten = _gameTiming.CurTime;
        }

        #region Buffs

        private void OnExamined(EntityUid uid, MyrmexHungerComponent comp, ref ExaminedEvent args)
        {
            var buff = MyrmexBuff.MultiplyBuffs(comp.Buffs);
            args.PushMarkup(Loc.GetString("medieval-myrmex-buff-health-examine", ("value", Math.Round(buff.Health, 2))));
            args.PushMarkup(Loc.GetString("medieval-myrmex-buff-damage-examine", ("value", Math.Round(buff.Damage, 2))));
            args.PushMarkup(Loc.GetString("medieval-myrmex-buff-stamina-examine", ("value", Math.Round(buff.Stamina, 2))));
        }

        private void OnModifyStaminaDamage(EntityUid uid, MyrmexHungerComponent comp, StaminaModifyEvent args)
        {
            var buff = MyrmexBuff.MultiplyBuffs(comp.Buffs);

            args.Damage *= buff.Stamina;
        }

        private void OnGetDamage(EntityUid uid, MyrmexHungerComponent comp, ref GetMeleeDamageEvent args)
        {
            var buff = MyrmexBuff.MultiplyBuffs(comp.Buffs);
            args.Damage *= buff.Damage;
        }

        private void OnGetDamageModifiers(EntityUid uid, MyrmexHungerComponent comp, ref DamageModifyEvent args)
        {
            var buff = MyrmexBuff.MultiplyBuffs(comp.Buffs);
            args.Damage *= buff.Health;
        }

        #endregion

        private void OnSpeedRefresh(EntityUid uid, MyrmexHungerComponent comp, RefreshMovementSpeedModifiersEvent args)
        {
            var curTime = _gameTiming.CurTime;
            var diff = (curTime - comp.LastEaten);

            if ((diff.HasValue && diff.Value.Duration() > TimeSpan.FromSeconds(comp.SecondsToHungry)))
            {
                _alertsSystem.ShowAlert(uid, "MyrmexHungry");
                args.ModifySpeed(comp.HungrySpeedModifier, comp.HungrySpeedModifier);
            }
            else
            {
                _alertsSystem.ClearAlert(uid, "MyrmexHungry");
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<MyrmexHungerComponent>();
            while (query.MoveNext(out var uid, out var hunger))
            {
                _speedModifier.RefreshMovementSpeedModifiers(uid);
            }
        }
    }
}
