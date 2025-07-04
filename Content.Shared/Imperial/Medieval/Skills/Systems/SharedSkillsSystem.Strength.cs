using System.Linq;
using Content.Shared.Cuffs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string StrengthId = "Strength";

    private void InitializeCombat()
    {
        SubscribeLocalEvent<SkillsComponent, GetMeleeDamageEvent>(OnGetDamage);
        SubscribeLocalEvent<SkillsComponent, ModifyUncuffDurationEvent>(OnModifyUncuffDuration);
        SubscribeLocalEvent<SkillsComponent, WieldAttemptEvent>(OnWieldAttempt);
    }

    private void OnGetDamage(EntityUid uid, SkillsComponent comp, ref GetMeleeDamageEvent args)
    {
        if (!args.RaisedOnUser)
            return;

        var (proto, level) = GetSkill(uid, StrengthId);

        if (level == 10)
            return;

        var positive = level > 10;

        var diff = Math.Abs(level - 10);

        var modifier = positive switch
        {
            true => (args.Weapon == args.User ? proto.Modifiers["FistPositiveDamageModifier"] : proto.Modifiers["WeaponPositiveDamageModifier"]) * diff,
            false => (args.Weapon == args.User ? proto.Modifiers["FistNegativeDamageModifier"] : proto.Modifiers["WeaponNegativeDamageModifier"]) * diff
        };

        args.Damage *= 1 + modifier;

        if (level >= 20)
            args.Damage *= 1 + (args.Weapon == args.User ? proto.Modifiers["FistMaxDamageModifier"] : proto.Modifiers["WeaponMaxDamageModifier"]);

        if (level > 16 && args.Damage.DamageDict.ContainsKey("Structural"))
            args.Damage.DamageDict["Structural"] *= 1.35f;
    }

    private void OnModifyUncuffDuration(EntityUid uid, SkillsComponent comp, ref ModifyUncuffDurationEvent args)
    {
        if (args.User != uid || args.Target != uid)
            return;

        var (proto, level) = GetSkill(uid, StrengthId);

        if (level == 10)
            return;

        var positive = level > 10;

        var diff = Math.Abs(level - 10);

        var modifier = positive switch
        {
            true => proto.Modifiers["PositiveUncuffDurationModifier"] * diff,
            false => proto.Modifiers["NegativeUncuffDurationModifier"] * diff
        };

        args.Duration *= Math.Clamp(1 + modifier, 0.2f, 3f);
    }

    private void OnWieldAttempt(EntityUid uid, SkillsComponent comp, ref WieldAttemptEvent args)
    {
        if (args.User != uid)
            return;

        var (_, level) = GetSkill(uid, StrengthId);

        if (level >= 5)
            return;

        args.Cancel();
        _popup.PopupPredicted("Вы слишком слабы, чтобы использовать это.", null, args.User, args.User, PopupType.Medium);
    }
}
