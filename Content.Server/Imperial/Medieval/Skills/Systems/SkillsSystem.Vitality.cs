using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Medieval.Body;
using Content.Server.Imperial.Medieval.NeedSleep;
using Content.Server.Popups;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Imperial.Medieval.Body;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeVitality()
    {
        SubscribeLocalEvent<SkillsComponent, GetSleepLevelModifiersEvent>(OnGetSleepModifiers);
        SubscribeLocalEvent<SkillsComponent, GetSuffocationDamageModifiersEvent>(OnModifySuffocationDamage);
        SubscribeLocalEvent<SkillsComponent, GetBloodRegenModifiersEvent>(OnModifyBloodRegen);

    }

    private void OnGetSleepModifiers(EntityUid uid, SkillsComponent comp, ref GetSleepLevelModifiersEvent args)
    {
        if (!args.Sleeping)
            return;

        var (proto, level) = GetSkill(uid, VitalityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveSleepEffeciencyModifier"] : proto.Modifiers["NegativeSleepEffeciencyModifier"]) * diff;
    }

    private void OnModifySuffocationDamage(EntityUid uid, SkillsComponent comp, ref GetSuffocationDamageModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveSuffocationDamageModifier"] : proto.Modifiers["NegativeSuffocationDamageModifier"]) * diff;
    }

    private void OnModifyBloodRegen(EntityUid uid, SkillsComponent comp, ref GetBloodRegenModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, VitalityId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveBloodRegenModifier"] : proto.Modifiers["NegativeBloodRegenModifier"]) * diff;
    }

    private void VitalityLevelSet(EntityUid uid, int level, int oldLevel)
    {
        var (proto, _) = GetSkill(uid, VitalityId);

        var diff = level - oldLevel;

        Comp<SkillsComponent>(uid).Timers.Remove(VitalityId);
        if (level <= 1)
            Comp<SkillsComponent>(uid).Timers.Add(VitalityId, _timing.CurTime + TimeSpan.FromSeconds(30f));

        if (TryComp<SoftCritEmotesComponent>(uid, out var crit))
        {
            crit.MinDamage += proto.Modifiers["AliveHealthPerLevel"] * diff;
            crit.Emote = level <= 16;
        }

        if (level > 16)
        {
            RemComp<EmoteOnDamageComponent>(uid);
            RemComp<DamageForceSayComponent>(uid);
        }

        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        foreach (var item in thresholds.Thresholds.Reverse())
        {
            if (item.Value is MobState.Alive or MobState.Invalid)
                continue;

            _threshold.SetMobStateThreshold(uid,
                                            item.Key + proto.Modifiers["AliveHealthPerLevel"] * diff,
                                            item.Value);
        }

        // if (!thresholds.Thresholds.Values.Contains(MobState.Wounded))
        //     return;

        // var toAdd = 0f;
        // if (level >= 20)
        //     toAdd += proto.Modifiers["MaxHealthBonus"];
        // else if (oldLevel >= 20 && level < 20)
        //     toAdd -= proto.Modifiers["MaxHealthBonus"];

        // _threshold.SetMobStateThreshold(uid,
        //                                 _threshold.GetThresholdForState(uid, MobState.Dead) + toAdd,
        //                                 MobState.Dead);

        // _threshold.SetMobStateThreshold(uid,
        //                                 _threshold.GetThresholdForState(uid, MobState.Critical) + toAdd,
        //                                 MobState.Critical);

        // _threshold.SetMobStateThreshold(uid,
        //                                 _threshold.GetThresholdForState(uid, MobState.Wounded) + toAdd,
        //                                 MobState.Wounded);
    }

    private void UpdateVitality(float frameTime)
    {
        var query = EntityQueryEnumerator<SkillsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Timers.TryGetValue(VitalityId, out var timer) || _timing.CurTime < timer)
                continue;

            if (GetSkill(uid, VitalityId).Item2 > 1)
                continue;

            comp.Timers[VitalityId] = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(30f, 60f));

            var i = _random.Next(0, 2);
            switch (i)
            {
                case 0:
                    _chat.TrySendInGameICMessage(uid, "кряхтит", InGameICChatType.Emote, false);
                    break;
                case 1:
                    _popup.PopupEntity("Спина болит...", uid, uid, Shared.Popups.PopupType.SmallCaution);
                    break;
                case 2:
                    _popup.PopupEntity("Ух, как же тяжело...", uid, uid, Shared.Popups.PopupType.SmallCaution);
                    break;
                default:
                    break;
            };

        }
    }
}
