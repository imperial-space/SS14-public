using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Imperial.Heretic.Paths.Cosmos;
using Content.Shared.Imperial.Heretic.Paths.Lock;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Rust;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Хватка Мансуса и метки путей.
/// </summary>
public sealed partial class HereticSystem
{
    private void OnMansusGrasp(EntityUid uid, HereticComponent comp, HereticMansusGraspActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        // SS13: on_grasp_cast — если в активной руке тёмный клинок и есть знание, вливаем силу вместо обычной Хватки
        if (comp.CurrentPath == HereticPath.Blade && comp.ResearchedKnowledge.Contains("KnowledgeEmpoweredBlades"))
        {
            var activeItem = _hands.GetActiveItem(uid);
            if (activeItem != null && TryComp<HereticBladeBladeComponent>(activeItem.Value, out var bladeComp) && !bladeComp.Infused)
            {
                bladeComp.Infused = true;
                Dirty(activeItem.Value, bladeComp);
                _appearance.SetData(activeItem.Value, HereticBladeSunderedVisuals.Infused, true);
                foreach (var held in _hands.EnumerateHeld(uid))
                {
                    if (held == activeItem.Value) continue;
                    if (!TryComp<HereticBladeBladeComponent>(held, out var offBladeComp) || offBladeComp.Infused) continue;
                    offBladeComp.Infused = true;
                    Dirty(held, offBladeComp);
                    _appearance.SetData(held, HereticBladeSunderedVisuals.Infused, true);
                }
                _popup.PopupEntity(Loc.GetString("heretic-empowered-blades-infused"), uid, uid, PopupType.Medium);
                return; // SS13: COMPONENT_CAST_HANDLESS — поглощает каст Хватки
            }
        }

        // Если уже держит предмет Хватки Мансуса — отменить
        if (TryFindAndDeleteMansusGraspItem(uid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-grasp-cancelled"), uid, uid);
            return;
        }

        var item = Spawn("HereticMansusGraspItem", Transform(uid).Coordinates);
        if (!_hands.TryPickupAnyHand(uid, item))
        {
            QueueDel(item);
            _popup.PopupEntity(Loc.GetString("heretic-grasp-hands-full"), uid, uid);
        }
    }

    private bool TryFindAndDeleteMansusGraspItem(EntityUid uid)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!HasComp<HereticMansusGraspItemComponent>(held))
                continue;

            RemComp<UnremoveableComponent>(held);
            QueueDel(held);
            return true;
        }
        return false;
    }

    private void OnMansusGraspItemSuicide(EntityUid uid, HereticMansusGraspItemComponent itemComp, SuicideByEnvironmentEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(uid))
            return;

        var victim = args.Victim;
        if (!HasComp<HereticComponent>(victim) || !HasComp<DamageableComponent>(victim))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("heretic-grasp-suicide"), victim, PopupType.LargeCaution);
        _chat.TryEmoteWithChat(victim, "Scream", ChatTransmitRange.Normal, ignoreActionBlocker: true);

        DoMansusGraspSuicideTick(victim, 0);
    }

    private void DoMansusGraspSuicideTick(EntityUid victim, int tick)
    {
        if (tick > 20 || TerminatingOrDeleted(victim) || !_mobs.IsAlive(victim))
            return;

        if (_random.Prob(0.7f))
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Heat"] = FixedPoint2.New(20);
            _damageSystem.TryChangeDamage(victim, dmg, ignoreResistances: true);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sizzle.ogg"), victim);

            if (_random.Prob(0.5f))
            {
                _chat.TryEmoteWithChat(victim, "Scream", ChatTransmitRange.Normal, ignoreActionBlocker: true);
                _stuttering.DoStutter(victim, TimeSpan.FromSeconds(26), true);
            }
        }

        if (!_mobs.IsAlive(victim))
            return;

        Timer.Spawn(TimeSpan.FromSeconds(0.4), () => DoMansusGraspSuicideTick(victim, tick + 1));
    }

    private void OnMansusGraspItemAfterInteract(EntityUid uid, HereticMansusGraspItemComponent itemComp, AfterInteractEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var caster = args.User;
        if (!TryComp<HereticComponent>(caster, out var comp))
            return;

        if (args.Target is not {} target)
        {
            if (args.CanReach && !args.Handled)
            {
                args.Handled = true;
                RaiseLocalEvent(caster, new HereticStartSlowRuneDrawEvent());
            }
            return;
        }

        if (caster == target)
            return;

        // SS220: Lionhunter's Rifle — Хватка Мансуса по дальней цели мгновенно переносит к ней
        var canReach = args.CanReach;
        if (!canReach)
        {
            if (!comp.ResearchedKnowledge.Contains("KnowledgeLionhunterRifle"))
                return;
            if (!_mobs.IsAlive(target))
                return;

            _xform.SetCoordinates(caster, Transform(target).Coordinates);
            Spawn("HereticEffectMansusMarkBlade", Transform(caster).Coordinates);
        }

        args.Handled = true;

        // Mansus Grasp wipes heretic runes on touch, just like the verb-based erase but instant.
        if (HasComp<HereticRuneComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-grasp-rune-erased"), caster, caster, PopupType.Medium);
            Spawn("HereticEffectRuneFail", Transform(target).Coordinates);
            QueueDel(target);
            RemComp<UnremoveableComponent>(uid);
            QueueDel(uid);
            return;
        }

        if (comp.CurrentPath == HereticPath.Lock
            && comp.ResearchedKnowledge.Contains("KnowledgeLockwielder"))
        {
            // SS13: ismecha → eject + Paralyze(5s)
            if (TryComp<MechComponent>(target, out var mechComp))
            {
                var pilot = mechComp.PilotSlot.ContainedEntity;
                _mech.TryEject(target, mechComp);
                if (pilot.HasValue)
                    _stun.TryAddParalyzeDuration(pilot.Value, TimeSpan.FromSeconds(5));
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-lock-mech"), caster, caster, PopupType.Small);
                RemComp<UnremoveableComponent>(uid);
                QueueDel(uid);
                return;
            }

            // SS13: istype(airlock) → door.unbolt() then open
            if (HasComp<DoorComponent>(target))
            {
                if (TryComp<DoorBoltComponent>(target, out var boltComp))
                    _door.SetBoltsDown((target, boltComp), false);
                _door.TryOpen(target);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-lock-door"), caster, caster, PopupType.Small);
                if (comp.PassiveLevel >= 3)
                    _lockPassive.TryResetGraspCooldown(caster);
                RemComp<UnremoveableComponent>(uid);
                QueueDel(uid);
                return;
            }

            // SS13: istype(computer) → computer.authenticated = TRUE (GotEmaggedEvent.Access)
            var emagEvent = new GotEmaggedEvent(caster, EmagType.Access);
            RaiseLocalEvent(target, ref emagEvent);
            if (emagEvent.Handled)
            {
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-lock-console"), caster, caster, PopupType.Small);
                if (comp.PassiveLevel >= 3 && HasComp<EntityStorageComponent>(target))
                    _lockPassive.TryResetGraspCooldown(caster);
                RemComp<UnremoveableComponent>(uid);
                QueueDel(uid);
                return;
            }
        }

        if (!_mobs.IsAlive(target))
        {
            RemComp<UnremoveableComponent>(uid);
            QueueDel(uid);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
        if (comp.CurrentPath == HereticPath.Blade && comp.ResearchedKnowledge.Contains("KnowledgeGraspOfBlade"))
        {
            if (IsBackstabTarget(caster, target))
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);
        }
        else
        {
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(5), true);
        }

        foreach (var actionEnt in comp.GrantedActions)
        {
            if (TerminatingOrDeleted(actionEnt))
                continue;
            if (MetaData(actionEnt).EntityPrototype?.ID != "ActionHereticMansusGrasp")
                continue;
            _actions.SetCooldown(new Entity<ActionComponent?>(actionEnt, CompOrNull<ActionComponent>(actionEnt)), TimeSpan.FromSeconds(10));
            break;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), target);
        Spawn(GetMansusMarkEffectProto(comp.CurrentPath), Transform(target).Coordinates);
        ApplyGraspMark(caster, comp, target);
        if (HasGraspUpgrade(comp))
            ApplyGraspUpgrade(caster, comp, target);

        RemComp<UnremoveableComponent>(uid);
        QueueDel(uid);
    }

    private static string GetMansusMarkEffectProto(HereticPath path)
    {
        return path switch
        {
            HereticPath.Ash    => "HereticEffectMansusMarkAsh",
            HereticPath.Lock   => "HereticEffectMansusMarkLock",
            HereticPath.Flesh  => "HereticEffectMansusMarkFlesh",
            HereticPath.Void   => "HereticEffectMansusMarkVoid",
            HereticPath.Blade  => "HereticEffectMansusMarkBlade",
            HereticPath.Rust   => "HereticEffectMansusMarkRust",
            HereticPath.Cosmos => "HereticEffectMansusMarkCosmos",
            _                  => "HereticEffectMansusMarkAsh",
        };
    }

    /// <summary>
    /// Grasp of the Blade: the stun only triggers if the target is prone or facing away from the heretic.
    /// </summary>
    private bool IsBackstabTarget(EntityUid heretic, EntityUid target)
    {
        if (_standing.IsDown(target))
            return true;

        var toHeretic = _xform.GetWorldPosition(heretic) - _xform.GetWorldPosition(target);
        if (toHeretic.LengthSquared() < 0.001f)
            return false;

        var approachAngle = Angle.FromWorldVec(toHeretic);
        var targetFacing = _xform.GetWorldRotation(target);
        var angleDiff = (approachAngle - targetFacing).Reduced().FlipPositive();

        return angleDiff > Math.PI / 2;
    }

    /// <summary>
    /// Mark of the Blade: first hit confines the target to their current room by bolting nearby
    /// doors; a follow-up hit on an already-marked target removes the mark, unbolts the doors and
    /// grants the heretic one orbiting blade.
    /// </summary>
    private const float BladeMarkLockRadius = 8f;

    private void ApplyBladeMark(EntityUid heretic, EntityUid target)
    {
        var mark = EnsureComp<HereticBladeMarkComponent>(target);
        mark.Heretic = heretic;
        mark.LockedDoors.Clear();

        var coords = Transform(target).Coordinates;
        foreach (var door in _lookup.GetEntitiesInRange<DoorBoltComponent>(coords, BladeMarkLockRadius))
        {
            if (_door.TrySetBoltDown(door, true))
                mark.LockedDoors.Add(door);
        }

        Dirty(target, mark);
        _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-blade"), target, target, PopupType.SmallCaution);
    }

    // SS13: trigger_mark — consume existing mark, grant orbiting blade
    public bool TryTriggerBladeMark(EntityUid heretic, EntityUid target)
    {
        if (!TryComp<HereticBladeMarkComponent>(target, out var mark) || mark.Heretic != heretic)
            return false;
        RemoveBladeMark(target, mark);
        SpawnOrbitingBlade(heretic);
        _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-blade-removed"), target, target, PopupType.MediumCaution);
        return true;
    }

    public bool TryTriggerLockMark(EntityUid heretic, EntityUid target)
    {
        if (!TryComp<LockMarkComponent>(target, out var lockMark))
            return false;

        if (lockMark.IdCard.HasValue && TryComp<AccessComponent>(lockMark.IdCard.Value, out var idAccess))
        {
            idAccess.Tags.Clear();
            _access.SetAccessEnabled(lockMark.IdCard.Value, true, idAccess);
            Dirty(lockMark.IdCard.Value, idAccess);
        }

        RemComp<LockMarkComponent>(target);
        _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-lock-removed"), target, target, PopupType.LargeCaution);
        return true;
    }

    /// <summary>
    /// Empowered Blades: a regular knife hit (not the Mansus Grasp action) now strikes with both hands
    /// at once and, on cooldown, imbues the strike with the Mansus Grasp effect.
    /// </summary>
    public void TryEmpoweredBladesOnHit(EntityUid heretic, HereticComponent comp, EntityUid target, EntityUid weapon)
    {
        // SS13: on_blade_equipped → demolition_mod = 2.5 (base 1x already dealt, +1.5x bonus = 2.5x total)
        if (!HasComp<MobStateComponent>(target))
        {
            if (TryComp<MeleeWeaponComponent>(weapon, out var weaponMelee) && HasComp<DamageableComponent>(target))
            {
                var bonusDmg = weaponMelee.Damage * 1.5f;
                _damageSystem.TryChangeDamage(target, bonusDmg, ignoreResistances: true);
            }
            return;
        }

        if (!_mobs.IsAlive(target) || target == heretic)
            return;

        // SS13: afterattack — infused hit applies blade mark then de-infuses; backstab adds paralysis + brute
        if (TryComp<HereticBladeBladeComponent>(weapon, out var bladeComp) && bladeComp.Infused)
        {
            ApplyBladeMark(heretic, target);
            bladeComp.Infused = false;
            Dirty(weapon, bladeComp);
            _appearance.SetData(weapon, HereticBladeSunderedVisuals.Infused, false);

            if (IsBackstabTarget(heretic, target))
            {
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(1.5), true);
                var backstabDmg = new DamageSpecifier();
                backstabDmg.DamageDict["Blunt"] = FixedPoint2.New(10);
                _damageSystem.TryChangeDamage(target, backstabDmg, ignoreResistances: true);
                _popup.PopupEntity(Loc.GetString("heretic-blade-backstab"), heretic, heretic, PopupType.Medium);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), heretic);
            }
        }

        // SS13: do_melee_effects — follow-up offhand attack with 0.25s delay (only when offhand blade exists)
        EntityUid? offhandBlade = null;
        foreach (var held in _hands.EnumerateHeld(heretic))
        {
            if (held == weapon) continue;
            if (!_tag.HasTag(held, HereticBladeTag)) continue;
            offhandBlade = held;
            break;
        }

        if (offhandBlade == null)
            return;

        var offhand = offhandBlade.Value;
        Timer.Spawn(TimeSpan.FromSeconds(0.25), () =>
        {
            if (TerminatingOrDeleted(heretic) || TerminatingOrDeleted(target) || TerminatingOrDeleted(offhand))
                return;
            if (!_mobs.IsAlive(target)) return;
            if (!TryComp<MeleeWeaponComponent>(offhand, out var offMelee)) return;

            var dmg = offMelee.Damage * 0.8f;
            _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);
            if (offMelee.HitSound != null)
                _audio.PlayPvs(offMelee.HitSound, heretic);
        });
    }

    private void RemoveBladeMark(EntityUid target, HereticBladeMarkComponent mark)
    {
        foreach (var door in mark.LockedDoors)
        {
            if (Exists(door) && TryComp<DoorBoltComponent>(door, out var boltComp))
                _door.TrySetBoltDown((door, boltComp), false);
        }

        RemComp<HereticBladeMarkComponent>(target);
    }

    public void ApplyGraspMark(EntityUid heretic, HereticComponent comp, EntityUid target)
    {
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
            {
                _blindable.AdjustEyeDamage((target, null), 9);
                _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
                    target, TemporaryBlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(20), true);
                _flammable.AdjustFireStacks(target, 3f, ignite: true);
                EnsureComp<AshMarkComponent>(target);
                break;
            }
            case HereticPath.Moon:
            {
                // Скрыть еретика на 5 секунд (moon_grasp_hide из SS13)
                var moonStealth = EnsureComp<StealthComponent>(heretic);
                _stealth.SetEnabled(heretic, true, moonStealth);
                _stealth.SetVisibility(heretic, -1f, moonStealth);
                var lunacy = EnsureComp<HereticGraspLunacyStealthComponent>(heretic);
                lunacy.EndTime = _timing.CurTime + lunacy.Duration;

                // Цель: галлюцинации 20 сек + -30 рассудка
                _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(20));
                _moonBrainDamage.AddBrainDamage(target, 30f);
                EnsureComp<MoonMarkComponent>(target);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-moon"), target, target, PopupType.SmallCaution);
                break;
            }
            case HereticPath.Lock:
            {
                var lockMark = EnsureComp<LockMarkComponent>(target);
                if (_inventory.TryGetSlotEntity(target, "id", out var idSlotItem))
                {
                    var cardId = idSlotItem.Value;
                    if (TryComp<PdaComponent>(idSlotItem, out var pda) && pda.ContainedId.HasValue)
                        cardId = pda.ContainedId.Value;

                    if (TryComp<AccessComponent>(cardId, out var idAccess))
                    {
                        lockMark.IdCard = cardId;
                        _access.SetAccessEnabled(cardId, false, idAccess);
                    }
                }
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-lock"), target, target, PopupType.SmallCaution);
                break;
            }
            case HereticPath.Void:
                if (TryComp<StatusEffectsComponent>(target, out var voidSe))
                    _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(10), true, voidSe);
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(3), true);
                var voidGraspDmg = new DamageSpecifier();
                voidGraspDmg.DamageDict["Blunt"] = FixedPoint2.New(10);
                _damageSystem.TryChangeDamage(target, voidGraspDmg, ignoreResistances: false);
                _hereticEffects.ApplyVoidChill(target, 2);
                EnsureComp<VoidMarkComponent>(target);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-void"), target, target, PopupType.SmallCaution);
                break;
            case HereticPath.Blade:
                if (comp.ResearchedKnowledge.Contains("KnowledgeSanguineSurge"))
                    _bloodstream.TryModifyBleedAmount(target, 5f);
                if (comp.ResearchedKnowledge.Contains("KnowledgeMarkOfBlade"))
                    ApplyBladeMark(heretic, target);
                break;
            case HereticPath.Rust when comp.ResearchedKnowledge.Contains("KnowledgeCorrode"):
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Caustic"] = FixedPoint2.New(10);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);

                if (comp.ResearchedKnowledge.Contains("KnowledgeMarkOfRust") && !HasComp<RustMarkComponent>(target))
                {
                    EnsureComp<RustMarkComponent>(target);
                    _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-rust"), target, target, PopupType.SmallCaution);
                }
                break;
            }
            case HereticPath.Cosmos:
            {
                if (HasComp<HereticComponent>(target) || HasComp<CosmosMarkImmuneComponent>(target))
                    break;
                var mark = EnsureComp<CosmosMarkComponent>(target);
                if (mark.AnchorEntity.HasValue && !TerminatingOrDeleted(mark.AnchorEntity.Value))
                    QueueDel(mark.AnchorEntity.Value);
                mark.AnchorEntity = Spawn("HereticCosmicDiamondAnchor", Transform(target).Coordinates);
                var capturedTarget = target;
                var capturedAnchor = mark.AnchorEntity.Value;
                Timer.Spawn(TimeSpan.FromSeconds(15), () =>
                {
                    if (Deleted(capturedTarget) || !TryComp<CosmosMarkComponent>(capturedTarget, out var m)) return;
                    if (m.AnchorEntity == capturedAnchor)
                    {
                        if (!TerminatingOrDeleted(capturedAnchor))
                            QueueDel(capturedAnchor);
                        RemCompDeferred<CosmosMarkComponent>(capturedTarget);
                    }
                });
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-cosmos"), target, target, PopupType.SmallCaution);
                break;
            }
        }
    }

    private bool HasGraspUpgrade(HereticComponent comp)
    {
        return comp.CurrentPath switch
        {
            HereticPath.Ash    => comp.ResearchedKnowledge.Contains("KnowledgeAshenPassage"),
            HereticPath.Lock   => false,
            HereticPath.Flesh  => false,
            HereticPath.Void   => false,
            HereticPath.Blade  => comp.ResearchedKnowledge.Contains("KnowledgeCleave"),
            HereticPath.Rust   => comp.ResearchedKnowledge.Contains("KnowledgeRustWave"),
            HereticPath.Cosmos => false,
            _                   => false,
        };
    }

    private void ApplyGraspUpgrade(EntityUid heretic, HereticComponent comp, EntityUid target)
    {
        _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(2));

        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Heat"] = FixedPoint2.New(15);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: true);
                break;
            }
            case HereticPath.Blade:
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Slash"] = FixedPoint2.New(15);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);
                _bloodstream.TryModifyBleedAmount(target, 10f);
                break;
            }
            case HereticPath.Rust:
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Caustic"] = FixedPoint2.New(15);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);
                break;
            }
        }
    }

    private void UpdateGraspLunacy()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticGraspLunacyStealthComponent>();
        while (query.MoveNext(out var uid, out var lunacy))
        {
            if (now < lunacy.EndTime)
                continue;

            if (TryComp<StealthComponent>(uid, out var stealth) && stealth.Enabled)
                _stealth.SetEnabled(uid, false, stealth);

            RemCompDeferred<HereticGraspLunacyStealthComponent>(uid);
        }
    }
}
