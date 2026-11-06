using Robust.Shared.Random;
using Content.Shared.Clothing.Components;
using Content.Shared.Hands;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Каза Рук (kaza_ruk.dm), корпоративное дзюдо (modular_bandastation/martial_arts/judo)
/// и прыжок-захват Укуса паука (component/tackler).
/// </summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly Content.Server.Imperial.Helpers.Schedule.ImperialScheduleSystem _schedule = default!;
    private const string JudoArmbar = "DDG";
    private const string JudoEyepoke = "DH";
    private const string JudoGoldenBlast = "EDEGDDGEDDGE";
    private const string JudoThrow = "GD";
    private const string JudoWheelThrow = "GDH";
    private const string JudoDiscombobulate = "discombobulate";

    /// <summary>status_effect/judo_armbar.</summary>
    private static readonly TimeSpan ArmbarDuration = TimeSpan.FromSeconds(5);

    /// <summary>tackler: stamina_cost, range, speed.</summary>
    private const float TackleStaminaCost = 20f;
    private const float TackleRange = 5f;
    private const float TackleSpeed = 6f;

    private readonly HashSet<EntityUid> _tackling = new();

    private static readonly TimeSpan KazaLowSweepTime = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan KazaNeckChopTime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan JudoArmbarMoveTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JudoEyepokeMoveTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan JudoGoldenBlastMoveTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JudoThrowMoveTime = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan JudoWheelThrowMoveTime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan JudoWheelThrowMoveTime2 = TimeSpan.FromSeconds(10);

    private const float KazaLowSweepAmount = 5f;
    private const int KazaLungPunchAmount = 5;
    private const float KazaLungPunchAmount2 = 10f;
    private const float KazaNeckChopAmount = 10f;
    private const float JudoArmbarMoveAmount = 45f;
    private const float JudoEyepokeMoveAmount = 10f;
    private const float JudoGoldenBlastMoveAmount = 120f;
    private const float JudoThrowMoveAmount = 25f;
    private const float JudoWheelThrowMoveAmount = 120f;

    private void InitializeKazaJudo()
    {
        SubscribeLocalEvent<MartialArtsComponent, DidEquipHandEvent>(OnJudoEquipHand);
        SubscribeLocalEvent<MartialArtsComponent, MartialArtsTackleActionEvent>(OnTackle);
        SubscribeLocalEvent<MartialArtsComponent, ThrowDoHitEvent>(OnTackleHit);
        SubscribeLocalEvent<MartialArtsComponent, LandEvent>(OnTackleLand);
    }

    #region Каза Рук

    private bool KazaCheckStreak(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        switch (ent.Comp.Streak)
        {
            case "neck_chop":
                ent.Comp.Streak = string.Empty;
                KazaNeckChop(ent, target);
                return true;
            case "low_sweep":
                ent.Comp.Streak = string.Empty;
                KazaLowSweep(ent, target);
                return true;
            case "quick_choke":
                ent.Comp.Streak = string.Empty;
                KazaLungPunch(ent, target);
                return true;
        }

        return false;
    }

    /// <summary>low_sweep(): подсечка ногой или хвостом.</summary>
    private void KazaLowSweep(EntityUid user, EntityUid target)
    {
        if (!IsConscious(target) || HasComp<Content.Shared.Stunnable.StunnedComponent>(target))
            return;

        var tail = HasTail(user) && IsHuman(user);
        PopupPair(user, target, tail ? "martial-kaza-tailsweep-others" : "martial-kaza-sweep-others",
            tail ? "martial-kaza-tailsweep-self" : "martial-kaza-sweep-self", "martial-kaza-sweep-target");
        Sound(user, "/Audio/Effects/hit_kick.ogg");
        Brute(target, KazaLowSweepAmount, user);
        Knockdown(target, KazaLowSweepTime);
    }

    /// <summary>quick_choke(): удар в грудь сбивает дыхание.</summary>
    private void KazaLungPunch(EntityUid user, EntityUid target)
    {
        PopupPair(user, target, "martial-kaza-lung-others", "martial-kaza-lung-self", "martial-kaza-lung-target");
        Sound(user, "/Audio/Weapons/punch2.ogg");
        LoseBreath(target, KazaLungPunchAmount);
        Oxy(target, KazaLungPunchAmount2, user);
    }

    /// <summary>neck_chop(): удар по горлу — 20 секунд немоты.</summary>
    private void KazaNeckChop(EntityUid user, EntityUid target)
    {
        PopupPair(user, target, "martial-kaza-chop-others", "martial-kaza-chop-self", "martial-kaza-chop-target");
        Sound(user, "/Audio/Weapons/punch2.ogg");
        Brute(target, KazaNeckChopAmount, user);
        Silence(target, KazaNeckChopTime);
    }

    private MartialResult KazaHarm(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (CheckBlock(target, ent, ent.Comp.KazaHarmDamage, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        if (KazaCheckStreak(ent, target))
            return MartialResult.Success;

        // blow_followup(): в половине обычных ударов — добивающий удар локтем через 0.25 секунды.
        if (_random.Prob(ent.Comp.KazaHarmChance))
        {
            _schedule.Schedule(ent.Comp.KazaHarmDelay, () =>
            {
                if (!Exists(ent) || !Exists(target) || !_xform.InRange(ent.Owner, target, MeleeRange))
                    return;

                var tail = HasTail(ent) && IsHuman(ent);
                PopupPair(ent, target, tail ? "martial-kaza-followup-tail-others" : "martial-kaza-followup-others",
                    tail ? "martial-kaza-followup-tail-self" : "martial-kaza-followup-self");
                Sound(ent, "/Audio/Weapons/punch3.ogg");
                Brute(target, MathF.Round(ArmDamageHigh / 3f), ent);
            });
        }

        return MartialResult.Invalid;
    }

    private MartialResult KazaDisarm(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        if (KazaCheckStreak(ent, target))
            return MartialResult.Success;

        if (_random.Prob(ent.Comp.KazaDisarmChance) && Disarm(target, ent) != null)
        {
            PopupPair(ent, target, "martial-kaza-disarm-others", "martial-kaza-disarm-self", "martial-kaza-disarm-target");
            Sound(target, "/Audio/Effects/thudswoosh.ogg");
        }

        return MartialResult.Invalid; // обычный толчок
    }

    #endregion

    #region Корпоративное дзюдо

    private bool JudoCheckOneClick(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (ent.Comp.Streak != JudoDiscombobulate)
            return false;

        ResetStreak(ent.Comp);
        PopupPair(ent, target, "martial-judo-discombobulate-others", "martial-judo-discombobulate-self", "martial-judo-discombobulate-target");
        Sound(ent, "/Audio/Imperial/MartialArts/slap.ogg");
        Stamina(target, ent.Comp.JudoCheckOneClickAmount, ent);
        Confuse(target, ent.Comp.JudoCheckOneClickTime);
        return true;
    }

    /// <summary>check_with_no_reset(): комбо должно стоять в конце строки.</summary>
    private static bool EndsWith(string streak, string combo) => streak.EndsWith(combo, StringComparison.Ordinal);

    private bool JudoCheckStreak(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var streak = ent.Comp.Streak;
        if (streak.Contains(JudoGoldenBlast))
        {
            ResetStreak(ent.Comp);
            return JudoGoldenBlastMove(ent, target);
        }

        // Должен проверяться раньше тычка в глаза.
        if (EndsWith(streak, JudoWheelThrow))
        {
            ResetStreak(ent.Comp);
            return JudoWheelThrowMove(ent, target);
        }

        if (EndsWith(streak, JudoArmbar))
            return JudoArmbarMove(ent, target);
        if (EndsWith(streak, JudoEyepoke))
            return JudoEyepokeMove(ent, target);
        if (EndsWith(streak, JudoThrow))
            return JudoThrowMove(ent, target);

        return false;
    }

    /// <summary>armbar(): взять лежачего в «рычаг».</summary>
    private bool JudoArmbarMove(EntityUid user, EntityUid target)
    {
        if (!IsLying(target))
            return false;

        PopupPair(user, target, "martial-judo-armbar-others", "martial-judo-armbar-self", "martial-judo-armbar-target");
        Sound(user, "/Audio/Imperial/MartialArts/slashmiss.ogg", -4f);
        if (!IsLying(user))
            DropAll(target);

        Stamina(target, JudoArmbarMoveAmount, user);
        EnsureComp<JudoArmbarComponent>(target).ExpiresAt = _timing.CurTime + ArmbarDuration;
        Knockdown(target, JudoArmbarMoveTime);
        return true;
    }

    /// <summary>eyepoke(): тычок в глаза, если их не закрывает очки или маска.</summary>
    private bool JudoEyepokeMove(EntityUid user, EntityUid target)
    {
        if (IsEyesProtected(target))
        {
            _popup.PopupEntity(Loc.GetString("martial-judo-eyes-protected"), user, user, PopupType.SmallCaution);
            return false;
        }

        PopupPair(user, target, "martial-judo-eyepoke-others", "martial-judo-eyepoke-self", "martial-judo-eyepoke-target");
        Sound(user, "/Audio/Imperial/MartialArts/whip.ogg", -4f);
        Brute(target, JudoEyepokeMoveAmount, user);
        Blind(target, JudoEyepokeMoveTime);
        return true;
    }

    /// <summary>goldenblast(): ЗОЛОТОЙ РАЗРЯД!</summary>
    private bool JudoGoldenBlastMove(EntityUid user, EntityUid target)
    {
        PopupPair(user, target, "martial-judo-golden-others", "martial-judo-golden-self", "martial-judo-golden-target", PopupType.LargeCaution);
        Sound(target, "/Audio/Weapons/Guns/Gunshots/taser.ogg");
        Say(user, Loc.GetString("martial-judo-golden-shout"));
        Sound(user, "/Audio/Imperial/MartialArts/goldenblast.ogg");
        Stamina(target, JudoGoldenBlastMoveAmount, user);
        Knockdown(target, JudoGoldenBlastMoveTime);
        Confuse(target, JudoGoldenBlastMoveTime);
        return true;
    }

    /// <summary>judothrow(): бросок на пол.</summary>
    private bool JudoThrowMove(EntityUid user, EntityUid target)
    {
        if (IsLying(user) || IsLying(target))
            return false;

        PopupPair(user, target, "martial-judo-throw-others", "martial-judo-throw-self", "martial-judo-throw-target");
        Sound(user, "/Audio/Imperial/MartialArts/slam.ogg", -4f);
        Stamina(target, JudoThrowMoveAmount, user);
        Knockdown(target, JudoThrowMoveTime);
        return true;
    }

    /// <summary>wheelthrow(): «Большое колесо» по взятому в рычаг.</summary>
    private bool JudoWheelThrowMove(EntityUid user, EntityUid target)
    {
        if (!IsLying(target) || !HasComp<JudoArmbarComponent>(target))
            return false;

        if (!IsLying(user))
        {
            PopupPair(user, target, "martial-judo-wheel-others", "martial-judo-wheel-self", "martial-judo-wheel-target");
            Sound(user, "/Audio/Imperial/MartialArts/tail_swing.ogg", -4f);
        }
        else
        {
            PopupPair(user, target, "martial-judo-pin-others", "martial-judo-pin-self", "martial-judo-pin-target");
            Sound(user, "/Audio/Imperial/MartialArts/slam.ogg", -4f);
        }

        Stamina(target, JudoWheelThrowMoveAmount, user);
        Knockdown(target, JudoWheelThrowMoveTime);
        Confuse(target, JudoWheelThrowMoveTime2);
        return true;
    }

    private MartialResult JudoHarm(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (JudoCheckOneClick(ent, target))
            return MartialResult.Success;

        AddToStreak(ent, ent.Comp, art, "H", target);
        if (JudoCheckStreak(ent, target))
            return MartialResult.Success;

        Brute(target, ent.Comp.JudoHarmAmount, ent);
        Sound(target, "/Audio/Weapons/punch2.ogg");
        PopupPair(ent, target, "martial-judo-chop-others", "martial-judo-chop-self", "martial-judo-chop-target", PopupType.Small);
        return MartialResult.Success;
    }

    private MartialResult JudoDisarm(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (JudoCheckOneClick(ent, target))
            return MartialResult.Success;

        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "D", target);
        return JudoCheckStreak(ent, target) ? MartialResult.Success : MartialResult.Invalid;
    }

    private MartialResult JudoGrab(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "G", target);
        return JudoCheckStreak(ent, target) ? MartialResult.Success : MartialResult.Invalid;
    }

    private void JudoHelp(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (JudoCheckOneClick(ent, target))
            return;

        AddToStreak(ent, ent.Comp, art, "E", target);
        JudoCheckStreak(ent, target);
    }

    /// <summary>check_baton(): поясу противна дубинка в руках.</summary>
    private void OnJudoEquipHand(Entity<MartialArtsComponent> ent, ref DidEquipHandEvent args)
    {
        if (GetActive(ent.Comp) is not { Kind: MartialArtKind.CorporateJudo } || !IsBaton(args.Equipped))
            return;

        _popup.PopupEntity(Loc.GetString("martial-judo-baton", ("item", args.Equipped)), ent, ent, PopupType.SmallCaution);
        _hands.TryDrop(ent.Owner, args.Equipped, checkActionBlocker: false);
    }

    private void JudoDropBatons(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (IsBaton(held))
                _hands.TryDrop(user, held, checkActionBlocker: false);
        }
    }

    private bool IsBaton(EntityUid item)
    {
        return HasComp<Content.Shared.Stunnable.StunbatonComponent>(item);
    }

    /// <summary>is_pepper_proof(): глаза закрыты очками или маской.</summary>
    private bool IsEyesProtected(EntityUid target)
    {
        foreach (var slot in new[] { "eyes", "mask", "head" })
        {
            if (_inventory.TryGetSlotEntity(target, slot, out var item)
                && HasComp<Content.Shared.Eye.Blinding.Components.EyeProtectionComponent>(item))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Прыжок-захват паука

    /// <summary>tackler: прыжок до пяти клеток, сбивающий с ног первого, в кого врежешься.</summary>
    private void OnTackle(Entity<MartialArtsComponent> ent, ref MartialArtsTackleActionEvent args)
    {
        if (args.Handled || IsLying(ent) || !IsConscious(ent))
            return;

        args.Handled = true;
        Stamina(ent, TackleStaminaCost);

        var from = _xform.GetWorldPosition(ent);
        var to = _xform.ToMapCoordinates(args.Target).Position;
        var dir = to - from;
        if (dir.Length() > TackleRange)
            dir = System.Numerics.Vector2.Normalize(dir) * TackleRange;

        _tackling.Add(ent);
        _throwing.TryThrow(ent, dir, TackleSpeed, ent, recoil: false, doSpin: false);
        PopupPair(ent, ent, "martial-spider-tackle-others", "martial-spider-tackle-self");
    }

    private void OnTackleHit(Entity<MartialArtsComponent> ent, ref ThrowDoHitEvent args)
    {
        if (!_tackling.Remove(ent) || !HasComp<Content.Shared.Mobs.Components.MobStateComponent>(args.Target))
            return;

        // skill_mod = 6: почти всегда сильный захват.
        PopupPair(ent, args.Target, "martial-spider-tackle-hit-others", "martial-spider-tackle-hit-self", "martial-spider-tackle-hit-target");
        Knockdown(args.Target, ent.Comp.TackleHitTime);
        Sound(ent, "/Audio/Effects/thudswoosh.ogg");
    }

    private void OnTackleLand(Entity<MartialArtsComponent> ent, ref LandEvent args)
    {
        if (_tackling.Remove(ent))
            Knockdown(ent, ent.Comp.TackleLandTime); // base_knockdown
    }

    #endregion
}
