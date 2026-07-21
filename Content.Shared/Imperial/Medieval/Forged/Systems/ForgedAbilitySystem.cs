using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Forged;
using Content.Shared.Imperial.DurabilityDisplay.Components;
using Content.Shared.Imperial.LocalLight;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;


namespace Content.Shared.Imperial.Medieval.Forged;

public sealed class ForgedAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedComponent, ForgedBloodEyesActionEvent>(OnBloodEyes);
        SubscribeLocalEvent<ForgedComponent, ForgedBoostActionEvent>(OnBoost);
        SubscribeLocalEvent<ForgedComponent, ForgedSilaActionEvent>(OnSila);
        SubscribeLocalEvent<ForgedComponent, ForgedRepairActionEvent>(OnRepair);
        SubscribeLocalEvent<ForgedComponent, ForgedExplosiveActionEvent>(OnExplosiveTrigger);
        SubscribeLocalEvent<ForgedComponent, ForgedInvisibilityNimbusActionEvent>(OnInvisibleNimbus);
        SubscribeLocalEvent<ForgedGunComponent, GunShotEvent>(OnGunShot);
    }

    public void ExecuteAbility(EntityUid forgedUid, EntityUid moduleUid, string abilityId)
    {
        switch (abilityId)
        {
            case "BloodVisionEyes":
                _actions.AddAction(forgedUid, "BloodEyesAction");
                break;
            case "MedicalEyes":
                MedicalEyes(forgedUid);
                break;
            case "InvisibleVisionEyes":
                InvisibleVisionEyes(forgedUid);
                break;
            case "NightVisionEyes":
                NightVisionEyes(forgedUid);
                break;
            case "Boost":
                CheckBoostReq(forgedUid);
                break;
            case "Sila":
                CheckSilaReq(forgedUid);
                break;
            case "Repair":
                _actions.AddAction(forgedUid, "RepairAction");
                break;
            case "VerySmart":
                VerySmart(forgedUid);
                break;
            case "Right_blade":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmBlade");
                break;
            case "Left_blade":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmBlade");
                break;
            case "Left_crossbow":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCrossbow");
                break;
            case "Right_crossbow":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCrossbow");
                break;
            case "Right_magic_gun_1":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCannon1");
                break;
            case "Left_magic_gun_1":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCannon1");
                break;
            case "Right_magic_gun_2":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCannon2");
                break;
            case "Left_magic_gun_2":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCannon2");
                break;
            case "Right_magic_gun_3":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCannon3");
                break;
            case "Left_magic_gun_3":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCannon3");
                break;
            case "Right_magic_gun_4":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCannon4");
                break;
            case "Left_magic_gun_4":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCannon4");
                break;
            case "Right_magic_gun_5":
                SpawnModuleInHand(forgedUid, "body_part_slot_right_hand", "ForgedArmCannon5");
                break;
            case "Left_magic_gun_5":
                SpawnModuleInHand(forgedUid, "body_part_slot_left_hand", "ForgedArmCannon5");
                break;
            case "Invisibility_Nimbus":
                _actions.AddAction(forgedUid, "InvisibileNimbusAction");
                break;
            case "Torso_Explosion":
                SetupExplosive(forgedUid);
                break;
            case "TransferMind":
                TransferMind(forgedUid, moduleUid);
                break;
            default:
                break;
        }
    }

    private void TransferMind(EntityUid forgedUid, EntityUid moduleUid)
    {
        if (_mindSystem.TryGetMind(forgedUid, out var oldMindId, out var oldMind))
        {
            _mindSystem.TransferTo(oldMindId, null, mind: oldMind);
        }

        if (_mindSystem.TryGetMind(moduleUid, out var targetMindId, out var targetMind))
        {
            _mindSystem.TransferTo(targetMindId, forgedUid, mind: targetMind);
        }
    }

    private void OnGunShot(Entity<ForgedGunComponent> ent, ref GunShotEvent args)
    {
        _hungerSystem.ModifyHunger(args.User, -ent.Comp.HungerCost);
    }
    private void SpawnModuleInHand(EntityUid forgedUid, string handId, string proto, bool strip = true)
    {
        if (!_containerSystem.TryGetContainer(forgedUid, handId, out var container))
            return;

        if (container.ContainedEntities.Count > 0)
        {
            var oldItem = container.ContainedEntities[0];
            _containerSystem.Remove(oldItem, container);
            QueueDel(oldItem);
        }

        var item = EntityManager.SpawnEntity(proto, MapCoordinates.Nullspace);
        if (strip)
        {
            RemComp<MedievalMeleeResourceComponent>(item);
            RemComp<DurabilityDisplayComponent>(item);
        }
        _containerSystem.Insert(item, container);
    }


    private void SetupExplosive(EntityUid forgedUid)
    {
        _actions.AddAction(forgedUid, "ExplosiveAction");
    }

    private void OnExplosiveTrigger(EntityUid uid, ForgedComponent comp, ForgedExplosiveActionEvent args)
    {
        if (_netManager.IsServer && args.Handled == false)
        {
            if (_gameTiming.CurTime - comp.LastExplosivePress < TimeSpan.FromSeconds(2))
            {
                RemComp<ShieldOnStartupComponent>(uid);

                var damage = new DamageSpecifier
                {
                    DamageDict = { ["Slash"] = FixedPoint2.New(10000) }
                };
                _damageable.TryChangeDamage(uid, damage);
                
                _explosionSystem.QueueExplosion(uid, "Default", 250, 5, 200);
                _actions.RemoveAction(uid, args.Action.Owner);
            }
            else
            {
                comp.LastExplosivePress = _gameTiming.CurTime;
                _popup.PopupEntity("Нажмите еще раз!", uid, uid);
            }
            Dirty(uid, comp);
        }

        args.Handled = true;
    }

    private void MedicalEyes(EntityUid forgedUid)
    {
        var comp1 = EnsureComp<ShowHealthBarsComponent>(forgedUid);
        var comp2 = EnsureComp<ShowHealthIconsComponent>(forgedUid);
        Dirty(forgedUid, comp1);
        Dirty(forgedUid, comp2);
    }
    private void InvisibleVisionEyes(EntityUid forgedUid)
    {
        var comp1 = EnsureComp<ShowThirstIconsComponent>(forgedUid);
        var comp2 = EnsureComp<SolutionScannerComponent>(forgedUid);
        Dirty(forgedUid, comp1);
        Dirty(forgedUid, comp2);
    }
    private void NightVisionEyes(EntityUid forgedUid)
    {
        if (!TryComp<LocalLightComponent>(forgedUid, out var comp)) return;
        comp.Radius = 10;
        Dirty(forgedUid, comp);
    }

    private void OnBloodEyes(EntityUid uid, ForgedComponent comp, ForgedBloodEyesActionEvent args)
    {
        if (args.Handled) return;

        if (HasComp<WerewolfBloodFeelComponent>(uid)) RemComp<WerewolfBloodFeelComponent>(uid);
        else EnsureComp<WerewolfBloodFeelComponent>(uid);

        if (!TryComp<ActionComponent>(args.Action, out var actionComponent)) return;
        _actions.SetToggled(args.Action.Owner, !actionComponent.Toggled);
        args.Handled = true;
    }

    private void OnInvisibleNimbus(EntityUid uid, ForgedComponent comp, ForgedInvisibilityNimbusActionEvent args)
    {
        if (args.Handled) return;

        if (HasComp<StealthComponent>(uid))
        {
            RemComp<StealthComponent>(uid);
            RemComp<StealthOnMoveComponent>(uid);

        }
        else
        {
            EnsureComp<StealthComponent>(uid);
            EnsureComp<StealthOnMoveComponent>(uid);
        }

        if (!TryComp<ActionComponent>(args.Action, out var actionComponent)) return;
        _actions.SetToggled(args.Action.Owner, !actionComponent.Toggled);
        args.Handled = true;
    }

    private void CheckBoostReq(EntityUid forgedUid)
    {
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "BoostReq")
            {
                _actions.AddAction(forgedUid, "BoostAction");
                return;
            }
        }
    }

    private void OnBoost(EntityUid forgedUid, ForgedComponent comp, ForgedBoostActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActionComponent>(args.Action, out var actionComponent)) return;
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "Boost")
            {
                bool isToggled = actionComponent.Toggled;

                if (!isToggled)
                {
                    module.SpeedModifier += 0.50f;
                    module.ResistanceModifier -= 1.25f;
                }
                else
                {
                    module.SpeedModifier -= 0.50f;
                    module.ResistanceModifier += 1.25f;
                }

                _movementSpeedModifier.RefreshMovementSpeedModifiers(forgedUid);
                _actions.SetToggled(args.Action.Owner, !isToggled);
                Dirty(forgedUid, forged);
            }
        }
        args.Handled = true;
    }

    private void CheckSilaReq(EntityUid forgedUid)
    {
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "SilaReq")
            {
                _actions.AddAction(forgedUid, "SilaAction");
                return;
            }
        }
    }

    private void OnSila(EntityUid forgedUid, ForgedComponent comp, ForgedSilaActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<ActionComponent>(args.Action, out var actionComponent)) return;
        if (!TryComp<ForgedComponent>(forgedUid, out var forged)) return;
        if (!TryComp<SkillsComponent>(forgedUid, out var skills)) return;

        foreach (var mod in forged.FittedModules)
        {
            if (TryComp<ForgedModuleComponent>(mod.Value, out var module) && module.AbilityId == "Boost")
            {
                bool isToggled = actionComponent.Toggled;

                if (!isToggled)
                {
                    skills.Levels["Strength"] = 20;
                    module.SpeedModifier -= 0.25f;
                }
                else
                {
                    skills.Levels["Strength"] = 10;
                    module.SpeedModifier += 0.25f;
                }

                _movementSpeedModifier.RefreshMovementSpeedModifiers(forgedUid);
                _actions.SetToggled(args.Action.Owner, !isToggled);
            }
        }
        Dirty(forgedUid, skills);
        Dirty(forgedUid, forged);
        args.Handled = true;
    }

    private void OnRepair(EntityUid forgedUid, ForgedComponent comp, ForgedRepairActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<DamageableComponent>(forgedUid, out var damageable)) return;

        FixedPoint2 healLeft = 40;

        var healSpecifier = new DamageSpecifier();

        foreach (var (damageType, amount) in damageable.Damage.DamageDict)
        {
            if (healLeft <= 0)
                break;

            if (amount <= 0)
                continue;

            var healAmount = FixedPoint2.Min(amount, healLeft);

            healSpecifier.DamageDict[damageType] = -healAmount;

            healLeft -= healAmount;
        }

        if (healSpecifier.DamageDict.Count > 0)
        {
            _damageable.TryChangeDamage(forgedUid, healSpecifier, ignoreResistances: true);
            _stun.TryKnockdown(forgedUid, TimeSpan.FromSeconds(10), true);
            _stun.TryAddParalyzeDuration(forgedUid, TimeSpan.FromSeconds(10));
        }
        args.Handled = true;
    }

    private void VerySmart(EntityUid forgedUid)
    {
        if (!TryComp<SkillsComponent>(forgedUid, out var skills)) return;

        skills.Levels["Intelligence"] = 20;

        Dirty(forgedUid, skills);
    }
}

