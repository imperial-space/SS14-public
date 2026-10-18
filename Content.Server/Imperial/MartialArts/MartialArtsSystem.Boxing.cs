using Robust.Shared.Random;
using System.Numerics;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Popups;

namespace Content.Server.Imperial.MartialArts;

/// <summary>datum/martial_art/boxing, boxing/evil, boxing/hunter (boxing.dm).</summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    /// <summary>unarmed_damage_low / high / effectiveness у человеческой руки.</summary>
    private const int ArmDamageLow = 5;
    private const int ArmDamageHigh = 10;
    private const float ArmEffectiveness = 10f;

    /// <summary>SKILL_EXP_LIST.</summary>
    private static readonly float[] AthleticsLevels = [0, 100, 250, 500, 900, 1500, 2500];

    /// <summary>SKILL_RANDS_MODIFIER атлетики по уровням.</summary>
    private static readonly float[] AthleticsRands = [0, 5, 10, 15, 20, 30, 50];

    /// <summary>CLICK_CD_MELEE.</summary>
    private static readonly TimeSpan ClickCooldown = TimeSpan.FromSeconds(0.8);

    private enum BoxingCombo : byte
    {
        None,
        StraightPunch,
        LightJab,
        LeftHook,
        RightHook,
        Uppercut,
        Discombobulate,
        BlindJab,
        CravenBlow,
    }

    private static readonly string[] HunterFirstWords =
        ["Вымирающим", "Брутализирующим", "Взрывным", "Приключенческим", "Громовым", "Молниеносным", "Сверхзвуковым", "Атомизирующим", "Вихривым", "Торнадо", "Акульим", "Сокольным"];

    private static readonly string[] HunterSecondWords =
        [" ударом", " джебом", " хуком", " кулаком", " апперкотом", " стрейтом", " страйком", " выпадом"];

    /// <summary>get_skill_level(athletics): уровни 1–7.</summary>
    private int AthleticsLevel(MartialArtsComponent comp)
    {
        var level = 1;
        for (var i = 0; i < AthleticsLevels.Length; i++)
        {
            if (comp.AthleticsExperience >= AthleticsLevels[i])
                level = i + 1;
        }

        return level;
    }

    private float AthleticsRandsModifier(EntityUid uid)
    {
        return TryComp<MartialArtsComponent>(uid, out var comp) ? AthleticsRands[AthleticsLevel(comp) - 1] : 0f;
    }

    private static bool Honorable(MartialArtKind kind) => kind != MartialArtKind.EvilBoxing;

    /// <summary>honor_check(): бить по правилам можно только другого боксёра.</summary>
    private bool BoxingHonorCheck(MartialArtKind kind, EntityUid target)
    {
        if (!Honorable(kind))
            return true;

        if (IsBoxingReady(target))
            return true;

        // Охотник бьёт ещё и зверьё, но не людей.
        return kind == MartialArtKind.HunterBoxing && !IsHuman(target);
    }

    /// <summary>TRAIT_BOXING_READY: у цели активен какой-то бокс.</summary>
    private bool IsBoxingReady(EntityUid uid)
    {
        return TryComp<MartialArtsComponent>(uid, out var comp)
               && GetActive(comp) is { Kind: MartialArtKind.Boxing or MartialArtKind.EvilBoxing or MartialArtKind.HunterBoxing };
    }

    private MartialResult BoxingGrab(Entity<MartialArtsComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("martial-boxing-no-grabs"), ent, ent, PopupType.SmallCaution);
        return MartialResult.Fail;
    }

    /// <summary>harm_act / disarm_act: единственный приём бокса — tussle().</summary>
    private MartialResult BoxingAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target, char button)
    {
        if (BoxingHonorCheck(art.Kind, target))
            AddToStreak(ent, ent.Comp, art, button.ToString(), target);

        Tussle(ent, art, target);
        return MartialResult.Success;
    }

    private void Tussle(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        var kind = art.Kind;

        // Хороший спортсмен лежачих не бьёт.
        if (Honorable(kind) && (IsLying(target) && IsStaminaCrit(target) || !IsConscious(target)))
        {
            _popup.PopupEntity(Loc.GetString("martial-boxing-unsporting"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var effectiveness = ArmEffectiveness;
        var damage = (float) _random.Next(ArmDamageLow, ArmDamageHigh + 1);
        var damageType = kind switch
        {
            MartialArtKind.HunterBoxing => "Blunt",
            MartialArtKind.EvilBoxing => "Blunt",
            _ => "Stamina",
        };

        var verb = "punch";
        var grantExperience = false;

        if (CheckBlock(target, ent, damage, MartialAttackType.Unarmed))
            return;

        if (BoxingHonorCheck(kind, target))
        {
            var strength = kind == MartialArtKind.EvilBoxing ? 2 : 0; // TRAIT_STRENGTH
            var combo = BoxingCheckStreak(ent, kind, target);
            var multiplier = 0f;
            switch (combo)
            {
                case BoxingCombo.StraightPunch:
                    verb = "straight";
                    multiplier = 1;
                    break;
                case BoxingCombo.LightJab:
                    verb = "jab";
                    multiplier = 1;
                    break;
                case BoxingCombo.LeftHook:
                    verb = "lefthook";
                    multiplier = 1.5f;
                    ent.Comp.NextAttackAllowed = _timing.CurTime + ClickCooldown * 1.5;
                    break;
                case BoxingCombo.RightHook:
                    verb = "righthook";
                    multiplier = 1.5f;
                    ent.Comp.NextAttackAllowed = _timing.CurTime + ClickCooldown * 1.5;
                    break;
                case BoxingCombo.Uppercut:
                    verb = "uppercut";
                    effectiveness *= 1.5f;
                    multiplier = 1;
                    ent.Comp.NextAttackAllowed = _timing.CurTime + ClickCooldown * 1.5;
                    break;
                case BoxingCombo.Discombobulate:
                    verb = "discombobulate";
                    Confuse(target, TimeSpan.FromSeconds(20));
                    multiplier = 1;
                    break;
                case BoxingCombo.BlindJab:
                    verb = "blindjab";
                    multiplier = 0.5f;
                    ent.Comp.NextAttackAllowed = _timing.CurTime + ClickCooldown * 1.5;
                    break;
                case BoxingCombo.CravenBlow:
                    verb = "sucker";
                    multiplier = 2;
                    Stagger(target, TimeSpan.FromSeconds(3));
                    break;
            }

            damage += MathF.Round((AthleticsLevel(ent.Comp) + strength) * multiplier);
            if (multiplier >= 1)
                BoxingExtraEffect(kind, ent, target);

            if (!_mobs.IsDead(target))
                grantExperience = true;
        }

        Sound(target, "/Audio/Weapons/punch1.ogg", -6f);
        PopupPair(ent, target, $"martial-boxing-{verb}-others", $"martial-boxing-{verb}-self", $"martial-boxing-{verb}-target");

        var experience = MathF.Round(damage / 4f);
        ApplyBoxingDamage(target, damage, damageType, ent);

        if (_mobs.IsDead(target) || !BoxingHonorCheck(kind, target))
            return;

        if (grantExperience)
            AddAthleticsExperience(ent.Comp, damage / ArmDamageLow);

        // Шанс нокаута: атлетика нападающего против атлетики защитника.
        var attackerSkill = AthleticsRandsModifier(ent) + effectiveness;
        var defenderSkill = Math.Clamp(AthleticsRandsModifier(target), 0, 100);
        var knockout = Math.Clamp(MathF.Round(attackerSkill - defenderSkill), 0, 100);
        if (!_random.Prob(knockout / 100f))
            return;

        BoxingCrit(ent, kind, target, damageType, damage);
        Sound(target, "/Audio/Imperial/MartialArts/coin2.ogg", -2f);
        AddAthleticsExperience(ent.Comp, experience * 2);
    }

    private void ApplyBoxingDamage(EntityUid target, float damage, string type, EntityUid origin)
    {
        if (type == "Stamina")
        {
            Stamina(target, damage, origin);
            return;
        }

        Brute(target, damage, origin, type);
    }

    /// <summary>check_streak() бокса: что вышло, зависит от кнопки и от активной руки.</summary>
    private BoxingCombo BoxingCheckStreak(Entity<MartialArtsComponent> ent, MartialArtKind kind, EntityUid target)
    {
        if (!Honorable(kind) && IsBehind(ent, target))
        {
            ResetStreak(ent.Comp);
            return BoxingCombo.CravenBlow;
        }

        if (IsDetective(ent) && IsBlind(target))
        {
            ResetStreak(ent.Comp);
            return BoxingCombo.Discombobulate;
        }

        var streak = ent.Comp.Streak;
        var left = IsLeftHandActive(ent);

        // «HH» — левая кнопка дважды, «DD» — правая дважды.
        if (streak.Contains("HH") || streak.Contains("DD"))
        {
            var matching = streak.Contains("HH") == left;
            ResetStreak(ent.Comp);
            if (matching)
                return BoxingCombo.StraightPunch;

            return IsBlind(ent) ? BoxingCombo.BlindJab : BoxingCombo.LightJab;
        }

        if (streak.Contains("DH") || streak.Contains("HD"))
        {
            ResetStreak(ent.Comp);
            if (left && streak.Contains("HD"))
                return BoxingCombo.LeftHook;
            if (!left && streak.Contains("DH"))
                return BoxingCombo.RightHook;

            // Та же связка другой рукой — апперкот.
            return BoxingCombo.Uppercut;
        }

        return BoxingCombo.None;
    }

    /// <summary>crit_effect(): ошеломить, а ошеломлённого — отправить в нокаут.</summary>
    private void BoxingCrit(EntityUid attacker, MartialArtKind kind, EntityUid target, string damageType, float damage)
    {
        if (kind == MartialArtKind.HunterBoxing && !IsHuman(target))
        {
            var words = _random.Pick(HunterFirstWords) + _random.Pick(HunterSecondWords);
            PopupPair(attacker, target, "martial-hunter-crit-others", "martial-hunter-crit-self", "martial-hunter-crit-target", PopupType.LargeCaution);
            Say(attacker, $"{words}!!!");
            Brute(target, damage * 2, attacker);

            // heal_overall_damage(healing_factor, healing_factor, healing_factor)
            var heal = MathF.Round(damage / 3f);
            _damage.HealDistributed(attacker, -FixedPoint2.New(heal * 3));
            return;
        }

        if (IsStaggered(target))
        {
            PopupPair(attacker, target, "martial-boxing-knockout-others", "martial-boxing-knockout-self", "martial-boxing-knockout-target", PopupType.LargeCaution);
            Knockdown(target, TimeSpan.FromSeconds(20));
            Sleep(target, TimeSpan.FromSeconds(10));
        }
        else
        {
            PopupPair(attacker, target, "martial-boxing-stagger-others", "martial-boxing-stagger-self", "martial-boxing-stagger-target");
            Stagger(target, TimeSpan.FromSeconds(3));
        }

        // Если держим агрессивно — ещё и отбрасываем.
        if (TryComp<MartialArtsComponent>(attacker, out var comp) && comp.GrabTarget == target && comp.GrabState >= MartialGrabState.Aggressive)
            ThrowAway(target, attacker, 2, 2);
    }

    /// <summary>perform_extra_effect(): охотник добивает зверьё ещё сильнее.</summary>
    private void BoxingExtraEffect(MartialArtKind kind, EntityUid attacker, EntityUid target)
    {
        if (kind != MartialArtKind.HunterBoxing || IsHuman(target))
            return;

        Brute(target, _random.Next(15, 21), attacker);
    }

    /// <summary>skill_experience_adjustment(): при обычной гравитации опыт делится на 2.</summary>
    private void AddAthleticsExperience(MartialArtsComponent comp, float value)
    {
        comp.AthleticsExperience += MathF.Round(value / 2f);
    }

    /// <summary>check_block() бокса: только безоружные удары и только в защитной стойке.</summary>
    private bool BoxingBlock(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid attacker, float damage, MartialAttackType type)
    {
        if (!ent.Comp.BlockMode || type != MartialAttackType.Unarmed || !IsConscious(ent))
            return false;

        if (!BoxingHonorCheck(art.Kind, attacker))
            return false;

        AddAthleticsExperience(ent.Comp, damage > 0 ? MathF.Round(damage / 4f) : 2);

        var chance = ArmEffectiveness + AthleticsRandsModifier(ent);
        if (!_random.Prob(chance / 100f))
            return false;

        if (_xform.InRange(ent.Owner, attacker, MeleeRange))
        {
            ApplyBoxingDamage(attacker, 10, art.Kind == MartialArtKind.HunterBoxing ? "Blunt" : "Stamina", ent);
            Stamina(ent, 5);
            BoxingExtraEffect(art.Kind, ent, attacker);
        }

        var dodge = _random.Prob(0.5f);
        PopupPair(ent, attacker, dodge ? "martial-boxing-avoid-others" : "martial-boxing-block-others", dodge ? "martial-boxing-avoid-self" : "martial-boxing-block-self");
        if (dodge)
            Sound(ent, "/Audio/Weapons/punchmiss.ogg", -6f);

        return true;
    }

    private bool IsLeftHandActive(EntityUid uid)
    {
        return TryComp<HandsComponent>(uid, out var hands)
               && hands.ActiveHandId is { } id
               && _hands.TryGetHand((uid, hands), id, out var hand)
               && hand.Value.Location == HandLocation.Left;
    }

    private bool IsBlind(EntityUid uid)
    {
        return TryComp<BlindableComponent>(uid, out var blind) && blind.IsBlind;
    }

    /// <summary>TRAIT_DETECTIVES_TASTE.</summary>
    private bool IsDetective(EntityUid uid)
    {
        return _mind.TryGetMind(uid, out var mindId, out _)
               && _jobs.MindTryGetJobId(mindId, out var job)
               && job == "Detective";
    }

    /// <summary>check_behind(): нападающий стоит у цели за спиной.</summary>
    private bool IsBehind(EntityUid attacker, EntityUid target)
    {
        var targetXform = Transform(target);
        var facing = _xform.GetWorldRotation(targetXform).ToWorldVec();
        var toAttacker = _xform.GetWorldPosition(attacker) - _xform.GetWorldPosition(targetXform);
        if (toAttacker.LengthSquared() < 0.001f)
            return false;

        return Vector2.Dot(Vector2.Normalize(toAttacker), facing) < -0.5f;
    }
}
