using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Clothing;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string VitalityId = "Vitality";

    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, DamageModifyEvent>(OnGetDamageModifiers);
    }

    private void OnGetDamageModifiers(EntityUid uid, SkillsComponent comp, ref DamageModifyEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level < 20)
            return;

        if (args.Damage.DamageDict.TryGetValue("Poison", out var poisonDamage) && poisonDamage > 0)
            args.Damage.DamageDict.Remove("Poison");
    }

    private void VitalityModifyClothingSpeedMod(EntityUid uid, SkillsComponent comp, ref ModifyClothingMovespeedModifierEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level <= 10)
            return;

        if (args.Walk >= 1f || args.Sprint >= 1f)
            return;

        var diff = Math.Abs(level - 10);
        args.Walk = Math.Clamp(args.Walk + proto.Modifiers["PositiveSlowdownModifier"] * diff, 0f, 1f);
        args.Sprint = Math.Clamp(args.Sprint + proto.Modifiers["PositiveSlowdownModifier"] * diff, 0f, 1f);
    }
}
