using Content.Shared.MeleeParry.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage;
using Robust.Shared.Random;
using Content.Shared.Timing;
using Content.Shared.Inventory;
using Content.Shared.Wieldable.Components;
using Content.Shared.Coordinates;
using Content.Shared.Popups;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Events;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared.MeleeParry
{
    public sealed partial class MeleeParrySystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
            SubscribeLocalEvent<MeleeParryEffectComponent, MapInitEvent>(OnStart);

        }

        private void OnStart(EntityUid uid, MeleeParryEffectComponent component, ref MapInitEvent args)
        {
            if (_netMan.IsServer)
                _popup.PopupEntity("Парирование", uid, PopupType.LargeCaution);
        }
        private void OnBeforeStaminaDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeStaminaDamageEvent args)
        { // Захардкожено для деревянного меча
            if (args.Value != 10)
                return;
            if (CheckParryChanceStamina(uid, component.ParryModifier))
            {
                args.Cancelled = true;
            }
        }

        public bool CheckParryChanceStamina(EntityUid uid, float modifier)
        { // Захардкожено для деревянного меча
            int oneHanded = 0;
            var itemH = _hands.GetActiveItem(uid);
            var item = itemH;
            if (_hands.GetActiveItem(uid) is null) return false;
            if (TryComp<MeleeWeaponComponent>(itemH, out var melee))
            {
                if (melee.ResetOnHandSelected)
                    oneHanded++;
                if (oneHanded > 1)
                    return false;
            }

            if (TryComp<MeleeParryComponent>(item, out var parry) &&
                TryComp<UseDelayComponent>(item, out var delay) &&
                !_useDelay.IsDelayed((item.Value, delay)) &&
                parry.ParriedAgo <= 0f &&
                TryComp<MeleeParryStaminaComponent>(item, out var stamina))
            {
                if (TryComp<WieldableComponent>(item, out var wield) && wield.Wielded)
                {
                    if (_random.Prob(parry.ParryChanse * modifier))
                    {
                        parry.ParriedAgo = parry.ParriedTime;
                        Spawn(parry.ParryEffect, parry.Owner.ToCoordinates());
                        return true;
                    }
                    else
                        return false;
                }
                if (!HasComp<WieldableComponent>(item))
                {
                    if (_random.Prob(parry.ParryChanse * modifier))
                    {
                        parry.ParriedAgo = parry.ParriedTime;
                        Spawn(parry.ParryEffect, parry.Owner.ToCoordinates());
                        return true;
                    }
                    else
                        return false;
                }

            }

            return false;

        }
        private void OnDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeDamageChangedEvent args)
        {
            if (args.Damage.GetTotal() < 4)
                return;
            if (!args.Damage.DamageDict.TryGetValue("ParryAble", out var parryDMG))
                return;
            if (args.Damage.GetTotal() > 36)
                return;
            if (CheckParryChance(uid, component.ParryModifier))
            {
                args.Cancelled = true;
            }

        }

        public bool CheckParryChance(EntityUid uid, float modifier)
        {
            int oneHanded = 0;
            var itemH = _hands.GetActiveItem(uid);
            var item = itemH;


            if (TryComp<MeleeWeaponComponent>(itemH, out var melee))
            {
                if (melee.ResetOnHandSelected)
                    oneHanded++;
                if (oneHanded > 1)
                    return false;
            }

            if (TryComp<MeleeParryComponent>(item, out var parry) && TryComp<UseDelayComponent>(item, out var delay) && !_useDelay.IsDelayed((item.Value, delay)) && parry.ParriedAgo <= 0f && parry.RealParry)
            {
                if (TryComp<WieldableComponent>(item, out var wield) && wield.Wielded)
                {
                    if (_random.Prob(parry.ParryChanse * modifier))
                    {
                        parry.ParriedAgo = parry.ParriedTime;
                        Spawn(parry.ParryEffect, parry.Owner.ToCoordinates());
                        //_audio.PlayPvs(new SoundPathSpecifier(parry.EffectSoundOnHit), uid, AudioParams.Default.WithVariation(0.15f));
                        //_audio.PlayEntity(new SoundPathSpecifier(parry.EffectSoundOnHit), Filter.Pvs(uid), uid, true, AudioParams.Default.WithVariation(0.15f));
                        return true;
                    }
                    else
                        return false;
                }
                if (!HasComp<WieldableComponent>(item))
                {
                    if (_random.Prob(parry.ParryChanse * modifier))
                    {
                        parry.ParriedAgo = parry.ParriedTime;
                        Spawn(parry.ParryEffect, parry.Owner.ToCoordinates());
                        //_audio.PlayEntity(new SoundPathSpecifier(parry.EffectSoundOnHit), Filter.Pvs(uid), uid, true, AudioParams.Default.WithVariation(0.15f));
                        return true;
                    }
                    else
                        return false;
                }

            }

            return false;

        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in EntityManager.EntityQuery<MeleeParryComponent>())
            {
                comp.ParriedAgo -= frameTime;
                if (comp.ParriedAgo < 0f)
                    comp.ParriedAgo = 0;
            }
            foreach (var compS in EntityManager.EntityQuery<MeleeParryStaminaComponent>())
            {
                compS.ParriedAgo -= frameTime;
                if (compS.ParriedAgo < 0f)
                    compS.ParriedAgo = 0;
            }
        }

    }
}
