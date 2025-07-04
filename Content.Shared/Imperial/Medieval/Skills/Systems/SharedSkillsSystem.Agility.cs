using Content.Shared.Imperial.Dash;
using Content.Shared.Imperial.Medieval.Climbing;
using Content.Shared.Imperial.Medieval.Inventory;
using Content.Shared.MedievalLockpick.Components;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string AgilityId = "Agility";

    private void InitializeAgility()
    {
        SubscribeLocalEvent<SkillsComponent, GetMeleeAttackRateEvent>(OnGetRate);
        SubscribeLocalEvent<SkillsComponent, CheckDashCooldownModifiersEvent>(OnGetDashCooldownModifiers);
        SubscribeLocalEvent<SkillsComponent, CheckDashDistanceModifiersEvent>(OnGetDashDistanceModifiers);
        SubscribeLocalEvent<SkillsComponent, CanDashEvent>(OnCanDash);
        SubscribeLocalEvent<SkillsComponent, GetClimbDelayModifiersEvent>(OnGetClimbDelayModifiers);
        SubscribeLocalEvent<SkillsComponent, GetLockpickChanceModifiersEvent>(OnGetLockpickModifiers);
        SubscribeLocalEvent<SkillsComponent, GetEquipDelayModifiersEvent>(OnGetEquipDelayModifiers);

    }

    private void OnGetRate(EntityUid uid, SkillsComponent comp, ref GetMeleeAttackRateEvent args)
    {
        if (!args.RaisedOnUser)
            return;

        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Multipliers += (level > 10 ? proto.Modifiers["PositiveAttackRate"] : proto.Modifiers["NegativeAttackRate"]) * diff;
    }

    private void OnGetDashCooldownModifiers(EntityUid uid, SkillsComponent comp, ref CheckDashCooldownModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveDashCooldownBonus"] : proto.Modifiers["NegativeDashCooldownBonus"]) * diff;
    }

    private void OnGetDashDistanceModifiers(EntityUid uid, SkillsComponent comp, ref CheckDashDistanceModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveDashDistanceBonus"] : proto.Modifiers["NegativeDashDistanceBonus"]) * diff;
    }

    private void OnCanDash(EntityUid uid, SkillsComponent comp, ref CanDashEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level > 1)
            return;

        args.Cancelled = true;
    }

    private void OnGetClimbDelayModifiers(EntityUid uid, SkillsComponent comp, ref GetClimbDelayModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveClimbDelayModifier"] : proto.Modifiers["NegativeClimbDelayModifier"]) * diff;
    }

    private void OnGetLockpickModifiers(EntityUid uid, SkillsComponent comp, ref GetLockpickChanceModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level <= 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveLockpickChanceModifier"] : proto.Modifiers["NegativeLockpickChanceModifier"]) * diff;
    }

    private void OnGetEquipDelayModifiers(EntityUid uid, SkillsComponent comp, ref GetEquipDelayModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, AgilityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveEquipDelayModifier"] : proto.Modifiers["NegativeEquipDelayModifier"]) * diff;
    }
}
