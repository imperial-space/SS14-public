using Content.Shared.MeleeParry.Components;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Content.Shared.Damage;
using Content.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Damage.Events;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Input.Binding;
using Content.Shared.Input;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;
using Content.Shared.Weapons.Melee;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Shared.Audio;

namespace Content.Shared.MeleeParry
{
    // Сетевое событие для передачи команды парирования с клиента на сервер
    [Serializable, NetSerializable]
    public sealed class ParryPressedEvent : EntityEventArgs { }

    public readonly struct ParryParameters
    {
        public readonly float Able;
        public readonly float Window;

        public ParryParameters(float able, float window)
        {
            Able = able;
            Window = window;
        }
    }

    public sealed partial class MeleeParrySystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly ISharedPlayerManager _playerManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedStaminaSystem _stamina = default!;

        private float _desyncTolerance;
        private float _parryReadySound;
        private readonly HashSet<EntityUid> _playedReadySounds = new();

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeDamageChangedEvent>(OnDamage);
            SubscribeLocalEvent<MeleeParryAbleComponent, BeforeStaminaDamageEvent>(OnStaminaDamage);

            SubscribeNetworkEvent<ParryPressedEvent>(ExecuteParryNetworked);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.MedievalMeleeParry, InputCmdHandler.FromDelegate(OnParryPressedLocal))
                .Register<MeleeParrySystem>();

            _cfg.OnValueChanged(CCVars.ParryDesyncTolerance, (value) => _desyncTolerance = value, true);
            _cfg.OnValueChanged(CCVars.ParryReadySoundType, (value) => _parryReadySound = value, true);
        }

        private void OnParryPressedLocal(ICommonSession? session)
        {
            if (!_netMan.IsClient) return;
            if (session?.AttachedEntity is not { } uid) return;

            if (CheckParryRequiments(uid, out var parryStorage, out var parry, out var item))
            {
                ExecuteParryLocal(uid, parry, parryStorage);
                return;
            }

            if (TryComp<MeleeParryStorageComponent>(uid, out var storage))
            {
                var timeLeft = (storage.NextParryTime - _timing.CurTime).TotalSeconds;

                if (timeLeft > 0 && timeLeft <= 0.15f)
                {
                    storage.ParryQueued = true;
                }
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (!_netMan.IsClient || _playerManager.LocalEntity is not { } localUid)
                return;

            if (TryComp<MeleeParryStorageComponent>(localUid, out var storage))
            {
                if (_timing.CurTime > storage.NextParryTime)
                {
                    if (storage.NextParryTime == TimeSpan.Zero)
                    {
                        _playedReadySounds.Add(localUid);
                        return;
                    }

                    if (!_playedReadySounds.Contains(localUid) && _timing.IsFirstTimePredicted)
                    {
                        if (_parryReadySound == 0) return;

                        var soundPath = _parryReadySound switch
                        {
                            1 => "/Audio/Imperial/Medieval/soft_bell_ding.ogg",
                            2 => "/Audio/Imperial/Medieval/pop.ogg",
                            3 => "/Audio/Imperial/Medieval/single_tick1.ogg",
                            4 => "/Audio/Imperial/Medieval/single_tick2.ogg",
                            _ => "/Audio/Imperial/Medieval/soft_bell_ding.ogg",
                        };

                        _audio.PlayGlobal(new SoundPathSpecifier(soundPath), Filter.Local(), false);
                        _playedReadySounds.Add(localUid);
                    }

                    if (storage.ParryQueued)
                    {
                        var item = _hands.GetActiveItem(localUid);
                        if (item != null && TryComp<MeleeParryComponent>(item.Value, out var parry))
                        {
                            storage.ParryQueued = false;
                            ExecuteParryLocal(localUid, parry, storage);
                        }
                    }
                }
                else
                {
                    if ((storage.NextParryTime - _timing.CurTime).TotalSeconds > 0.5)
                    {
                        _playedReadySounds.Remove(localUid);
                    }
                }
            }
        }

        private bool CheckParryRequiments(EntityUid uid, out MeleeParryStorageComponent parryStorage, out MeleeParryComponent parry, out EntityUid itemUid)
        {
            parryStorage = null!;
            parry = null!;
            itemUid = EntityUid.Invalid;

            if (TryComp<StaminaComponent>(uid, out var stam) && stam.Critical)
                return false;

            if (TryComp<MobStateComponent>(uid, out var mobState))
            {
                if (mobState.CurrentState is MobState.Critical or MobState.Dead)
                    return false;
            }

            if (!TryComp<MeleeParryStorageComponent>(uid, out var storageComp))
                return false;

            if (_netMan.IsServer)
                if (_timing.CurTime + TimeSpan.FromSeconds(_desyncTolerance) < storageComp.NextParryTime)
                    return false;
            if (_netMan.IsClient)
                if (_timing.CurTime < storageComp.NextParryTime)
                    return false;

            var item = _hands.GetActiveItem(uid);
            if (item == null || !TryComp<MeleeParryComponent>(item.Value, out var parryComp))
                return false;

            if (_useDelay.IsDelayed(item.Value))
                return false;

            parryStorage = storageComp;
            parry = parryComp;
            itemUid = item.Value;

            return true;
        }

        private void ExecuteParryLocal(EntityUid uid, MeleeParryComponent parry, MeleeParryStorageComponent parryStorage)
        {
            var cooldown = TimeSpan.FromSeconds(Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7.5f));
            var nextTime = _timing.CurTime + cooldown;

            if (!TryComp<MeleeWeaponComponent>(parry.Owner, out var weapon))
                return;
            weapon.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(parry.ParryUseDelay);

            parryStorage.NextParryTime = nextTime;
            parryStorage.CooldownParry = (float)cooldown.TotalSeconds;

            RaiseNetworkEvent(new ParryPressedEvent());
        }

        private void ExecuteParryNetworked(ParryPressedEvent args, EntitySessionEventArgs sessionArgs)
        {
            if (sessionArgs.SenderSession.AttachedEntity is not { } uid) return;

            if (CheckParryRequiments(uid, out var parryStorage, out var parry, out var item))
            {
                if (!TryComp<MeleeWeaponComponent>(item, out var weapon))
                    return;

                var useDelay = EnsureComp<UseDelayComponent>(item);

                var cooldown = TimeSpan.FromSeconds(Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7f));
                var nextTime = _timing.CurTime + cooldown;

                parryStorage.NextParryTime = nextTime;

                var latency = TimeSpan.FromMilliseconds(Math.Min(sessionArgs.SenderSession.Ping / 2, 400));

                parry.ParriedTime = _timing.CurTime - latency;
                parryStorage.CooldownParry = (float)cooldown.TotalSeconds;

                _useDelay.SetLength(item, TimeSpan.FromSeconds(parry.ParryUseDelay));
                _useDelay.TryResetDelay((item, useDelay));
                weapon.NextAttack = _timing.CurTime + TimeSpan.FromSeconds(parry.ParryUseDelay);

                Spawn(parry.ParryEffectWindow, Transform(uid).Coordinates);

                Dirty(item, weapon);
                Dirty(item, parry);
                Dirty(uid, parryStorage);
            }
            else
            {
                if (!TryComp<MeleeWeaponComponent>(item, out var weapon))
                    return;

                Dirty(item, weapon);
                Dirty(item, parry);
                Dirty(uid, parryStorage);
            }
        }

        private void OnStaminaDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeStaminaDamageEvent args)
        {
            var item = _hands.GetActiveItem(uid);
            if (!TryComp<MeleeParryComponent>(item, out var parry)) return;

            if (parry.LastSuccessParriedAttacker == args.Origin &&
               (_timing.CurTime - parry.LastSuccessParriedTime).TotalSeconds < 3f) // Перестраховка
            {
                parry.LastSuccessParriedAttacker = null;
                parry.LastSuccessParriedTime = TimeSpan.Zero;
                Dirty(item.Value, parry);
                args.Cancelled = true;
            }
        }

        private void OnDamage(EntityUid uid, MeleeParryAbleComponent component, ref BeforeDamageChangedEvent args)
        {
            if (args.Origin == null || !args.Damage.DamageDict.TryGetValue("ParryAble", out var parryDMG))
                return;

            var attacker = args.Origin.Value;
            var attackerItem = _hands.GetActiveItem(attacker);

            if (attackerItem != null && TryComp<MedievalWeaponSkillCategoryComponent>(attackerItem.Value, out var skillComp))
                parryDMG *= skillComp.Skill.GetParryData().Able;


            if (CheckParryable(uid, (float)parryDMG, out var item, out var parry, out var parryStorage, out var weapon))
            {
                args.Cancelled = true;

                parry.LastSuccessParriedAttacker = args.Origin;

                parry.LastSuccessParriedTime = _timing.CurTime;
                parry.ParriedTime = TimeSpan.Zero;


                parryStorage.NextParryTime = TimeSpan.Zero;
                parryStorage.CooldownParry = Math.Clamp(parry.ParryCooldown / (GetAgilityMod(uid) / 10f), 2.5f, 7f);
                parryStorage.ParryQueued = false;

                var useDelay = EnsureComp<UseDelayComponent>(item);
                _useDelay.SetLength(item, TimeSpan.Zero);
                _useDelay.TryResetDelay((item, useDelay));
                weapon.NextAttack = TimeSpan.Zero;

                Dirty(item, weapon);
                Dirty(uid, parryStorage);
                Dirty(item, parry);

                float staminaDMGBoost = 1f;
                if (TryComp<StaminaParryBoosterComponent>(uid, out var booster))
                    staminaDMGBoost *= booster.StaminaDamageMultiplier;

                var staminaDamage = parry.ParryStaminaDamage * staminaDMGBoost;
                _stamina.TakeStaminaDamage(args.Origin.Value, staminaDamage);

                if (weapon.Damage.GetTotal() > 4)
                    Spawn(parry.ParryEffectSuccess, Transform(uid).Coordinates);
                else
                    Spawn(parry.ParryEffectSuccess, Transform(uid).Coordinates);
            }
        }

        public bool CheckParryable(EntityUid uid, float parryDMG, out EntityUid weaponUid, out MeleeParryComponent parry, out MeleeParryStorageComponent parryStorage, out MeleeWeaponComponent weapon)
        {
            weaponUid = EntityUid.Invalid;
            parry = null!;
            parryStorage = null!;
            weapon = null!;

            var item = _hands.GetActiveItem(uid);
            if (item == null) return false;

            if (TryComp<MeleeParryComponent>(item, out var parryComp) &&
                parryComp.ParriedTime != TimeSpan.Zero &&
                CountParryWindowTime((item.Value, parryComp), parryDMG) > _timing.CurTime &&
                TryComp<MeleeParryStorageComponent>(uid, out var parryStorageComp) &&
                TryComp<MeleeWeaponComponent>(item, out var weaponComp))
            {
                weaponUid = item.Value;
                parry = parryComp;
                parryStorage = parryStorageComp;
                weapon = weaponComp;
                return true;
            }

            return false;
        }

        private TimeSpan CountParryWindowTime(Entity<MeleeParryComponent> ent, float parryDMG)
        {
            var parryWindow = ent.Comp.ParryWindow;
            if (TryComp<MedievalWeaponSkillCategoryComponent>(ent, out var skillComp))
                parryWindow *= skillComp.Skill.GetParryData().Window;

            return ent.Comp.ParriedTime + TimeSpan.FromSeconds(parryWindow * parryDMG);
        }
        private float GetAgilityMod(EntityUid uid)
        {
            if (TryComp<SkillsComponent>(uid, out var skills) && skills.Levels.TryGetValue("Agility", out var level))
                return Math.Max(level, 1f);
            return 1f;
        }
    }
}
