using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Coordinates;
using Robust.Shared.Player;
using Robust.Server.Player;
using Content.Server.MedievalMeleeResource.Components;

namespace Content.Server.MedievalMeleeResource
{
    public sealed partial class MedievalMeleeResourceSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MedievalMeleeResourceComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<MedievalMeleeResourceComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<MeleeWeaponComponent, ComponentStartup>(OnWeaponStart);
            SubscribeLocalEvent<MedievalMeleeRepairComponent, BeforeRangedInteractEvent>(OnUseInHand);
        }

        public void OnUseInHand(EntityUid uid, MedievalMeleeRepairComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUse(args.Target, args.User, args.Used, comp);
        }

        public void OnUse(EntityUid? target, EntityUid user, EntityUid used, MedievalMeleeRepairComponent comp)
        {
            if (target == null)
                return;
            if (TryComp<MedievalMeleeResourceComponent>(target, out var resource) && resource != null)
            {
                if (HasComp<MedievalMeleeRepairManComponent>(user))
                {
                    resource.Resource += comp.Resource * 2;
                }
                else
                {
                    resource.Resource += comp.Resource;
                    resource.ResourceWaste *= 1.05f;
                }

                if (resource.Resource > resource.MaxResource)
                    resource.Resource = resource.MaxResource;
                _audioSystem.PlayPvs(resource.EffectSoundOnRepair, target.Value);
                CheckResource(target.Value, resource);

                QueueDel(used);
            }
        }

        private void CheckResource(EntityUid uid, MedievalMeleeResourceComponent component)
        {
            if (component.Resource > 100f)
            {
                if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                {
                    weapon.Damage = component.UpDamage;
                }
                if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
                {
#pragma warning disable RA0002
                    wield.BonusDamage = component.UpWieldDamage;
#pragma warning restore RA0002
                }
                component.DamageState = "Up";
            }
            if (component.Resource > 80f && component.Resource <= 100f)
            {
                if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                {
                    weapon.Damage = component.FullDamage;
                }
                if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
                {
#pragma warning disable RA0002
                    wield.BonusDamage = component.FullWieldDamage;
#pragma warning restore RA0002
                }
                component.DamageState = "Full";
            }

            if (component.Resource > 60f && component.Resource <= 80f)
            {
                if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                {
                    weapon.Damage = component.AlmostFullDamage;
                }
                if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
                {
#pragma warning disable RA0002
                    wield.BonusDamage = component.AlmostFullWieldDamage;
#pragma warning restore RA0002
                }
                component.DamageState = "AlmostFull";
            }

            if (component.Resource > 40f && component.Resource <= 60f)
            {
                if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                {
                    weapon.Damage = component.DamagedDamage;
                }
                if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
                {
#pragma warning disable RA0002
                    wield.BonusDamage = component.DamagedWieldDamage;
#pragma warning restore RA0002
                }
                component.DamageState = "Damaged";
            }

            if (component.Resource > 20f && component.Resource <= 40f)
            {
                if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                {
                    weapon.Damage = component.BadlyDamagedDamage;
                }
                if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
                {
#pragma warning disable RA0002
                    wield.BonusDamage = component.BadlyDamagedWieldDamage;
#pragma warning restore RA0002
                }
                component.DamageState = "BadlyDamaged";
            }

            if (component.Resource > 0f && component.Resource <= 20f)
            {
                if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
                {
                    weapon.Damage = component.BrokenDamage;
                }
                if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
                {
#pragma warning disable RA0002
                    wield.BonusDamage = component.BrokenWieldDamage;
#pragma warning restore RA0002
                }
                component.DamageState = "Broken";
            }
            if (component.Resource == 0)
            {
                _audioSystem.PlayStatic(
                    component.EffectSoundOnBreak,
                    Filter.Pvs(uid.ToCoordinates(), 1, EntityManager, _playerManager),
                    uid.ToCoordinates(),
                    true
                );
                QueueDel(uid);
            }


        }

        private void OnWeaponStart(EntityUid uid, MeleeWeaponComponent component, ComponentStartup args)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon) && !HasComp<ExaminerComponent>(uid))
            {
                EnsureComp<MedievalMeleeResourceComponent>(uid);
            }
        }
        private void OnStart(EntityUid uid, MedievalMeleeResourceComponent component, ComponentStartup args)
        {
            //EnsureComp<MedievalItemRustComponent>(uid); временное выключение ржавчины

            if (TryComp<MeleeWeaponComponent>(uid, out var weapon))
            {
                component.UpDamage = weapon.Damage * component.UpModifier;
                component.FullDamage = weapon.Damage * component.FullModifier;
                component.AlmostFullDamage = weapon.Damage * component.AlmostFullModifier;
                component.DamagedDamage = weapon.Damage * component.DamagedModifier;
                component.BadlyDamagedDamage = weapon.Damage * component.BadlyDamagedModifier;
                component.BrokenDamage = weapon.Damage * component.BrokenModifier;

            }

            if (TryComp<IncreaseDamageOnWieldComponent>(uid, out var wield))
            {
                component.UpWieldDamage = wield.BonusDamage * component.UpModifier;
                component.FullWieldDamage = wield.BonusDamage * component.FullModifier;
                component.AlmostFullWieldDamage = wield.BonusDamage * component.AlmostFullModifier;
                component.DamagedWieldDamage = wield.BonusDamage * component.DamagedModifier;
                component.BadlyDamagedWieldDamage = wield.BonusDamage * component.BadlyDamagedModifier;
                component.BrokenWieldDamage = wield.BonusDamage * component.BrokenModifier;

            }

        }
        private void OnMeleeHit(EntityUid uid, MedievalMeleeResourceComponent component, MeleeHitEvent args)
        {
            if (TryComp<MeleeWeaponComponent>(args.Weapon, out var weapon))
            {
                component.Resource -= component.ResourceWaste * CheckBadTarget(uid, component, args);
                if (component.Resource < 0f)
                    component.Resource = 0f;
                if (component.Resource > component.MaxResource)
                    component.Resource = component.MaxResource;
                CheckResource(uid, component);
                // Full
                // AlmostFull
                // Damaged
                // BadlyDamaged
                // Broken

            }
        }
        public float CheckBadTarget(EntityUid uid, MedievalMeleeResourceComponent component, MeleeHitEvent args)
        {
            foreach (var hit in args.HitEntities)
            {
                if (TryComp<MedievalMeleeBadTargetComponent>(hit, out var target) && target.SafeToHitGroup != component.SafeToHitGroup)
                    return target.BreakMultiplier;
            }
            return 1.0f;
        }

    }
}
