using Robust.Shared.Random;
using Content.Shared.Imperial.MartialArts;
using Content.Server.Kitchen.Components;
using Content.Shared.Popups;

namespace Content.Server.Imperial.MartialArts;

/// <summary>datum/martial_art/cqc и cqc/under_siege (cqc.dm).</summary>
public sealed partial class MartialArtsSystem
{
    private const string CqcSlam = "GH";
    private const string CqcKick = "HH";
    private const string CqcRestrain = "GG";
    private const string CqcPressure = "DG";
    private const string CqcConsecutive = "DDH";

    /// <summary>block_chance.</summary>
    private const float CqcBlockChance = 0.75f;

    /// <summary>Сколько держится «сдерживание» (addtimer 5 SECONDS).</summary>
    private static readonly TimeSpan CqcRestrainTime = TimeSpan.FromSeconds(5);

    /// <summary>kitchen_areas: в SS14 нет зон, кухней считается окрестность кухонной техники.</summary>
    private const float KitchenRange = 6f;

    private bool CqcCheckStreak(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var streak = ent.Comp.Streak;
        if (streak.Contains(CqcSlam))
        {
            ResetStreak(ent.Comp);
            return CqcSlamMove(ent, target);
        }

        if (streak.Contains(CqcKick))
        {
            ResetStreak(ent.Comp);
            return CqcKickMove(ent, target);
        }

        if (streak.Contains(CqcRestrain))
        {
            ResetStreak(ent.Comp);
            return CqcRestrainMove(ent, target);
        }

        if (streak.Contains(CqcPressure))
        {
            ResetStreak(ent.Comp);
            return CqcPressureMove(ent, target);
        }

        if (streak.Contains(CqcConsecutive))
        {
            ResetStreak(ent.Comp);
            return CqcConsecutiveMove(ent, target);
        }

        return false;
    }

    /// <summary>Slam: Захват, Удар — швырнуть на пол.</summary>
    private bool CqcSlamMove(EntityUid user, EntityUid target)
    {
        if (IsLying(target))
            return false;

        PopupPair(user, target, "martial-cqc-slam-others", "martial-cqc-slam-self", "martial-cqc-slam-target");
        Sound(user, "/Audio/Imperial/MartialArts/slam.ogg");
        Brute(target, 10, user);
        Paralyze(target, TimeSpan.FromSeconds(12));
        return true;
    }

    /// <summary>Kick: Удар, Удар — отбросить; лежачего в стамкрите — пинком в голову.</summary>
    private bool CqcKickMove(EntityUid user, EntityUid target)
    {
        if (!IsConscious(target))
            return false;

        if (IsLying(target) && IsStaminaCrit(target))
        {
            PopupPair(user, target, "martial-cqc-headkick-others", "martial-cqc-headkick-self", "martial-cqc-headkick-target");
            Sound(user, "/Audio/Weapons/genhit1.ogg");
            Knockdown(target, TimeSpan.FromSeconds(20));
            Sleep(target, TimeSpan.FromSeconds(10));
            BrainDamage(target, 15);
        }
        else
        {
            PopupPair(user, target, "martial-cqc-kick-others", "martial-cqc-kick-self", "martial-cqc-kick-target");
            Sound(user, "/Audio/Imperial/MartialArts/cqchit1.ogg");
            ThrowAway(target, user, 1, 14);
            Brute(target, 10, user);
            if (IsLying(target))
                Stamina(target, 45, user);
        }

        return true;
    }

    /// <summary>Pressure: Толчок, Захват — сильный удар по выносливости.</summary>
    private bool CqcPressureMove(EntityUid user, EntityUid target)
    {
        PopupPair(user, target, "martial-cqc-pressure-others", "martial-cqc-pressure-self", "martial-cqc-pressure-target");
        Stamina(target, 60, user);
        Sound(user, "/Audio/Imperial/MartialArts/cqchit1.ogg");
        return true;
    }

    /// <summary>Restrain: Захват, Захват — держать и подготовить удушающий приём.</summary>
    private bool CqcRestrainMove(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (ent.Comp.Restraining != null || !IsConscious(target))
            return false;

        PopupPair(ent, target, "martial-cqc-restrain-others", "martial-cqc-restrain-self", "martial-cqc-restrain-target");
        Stamina(target, 20, ent);
        Stun(target, TimeSpan.FromSeconds(10));
        ent.Comp.Restraining = target;
        ent.Comp.RestrainingUntil = _timing.CurTime + CqcRestrainTime;
        return true;
    }

    /// <summary>Consecutive: Толчок, Толчок, Удар — живот, шея, спина и отнятый предмет.</summary>
    private bool CqcConsecutiveMove(EntityUid user, EntityUid target)
    {
        if (!IsConscious(target))
            return false;

        PopupPair(user, target, "martial-cqc-consecutive-others", "martial-cqc-consecutive-self", "martial-cqc-consecutive-target");
        Sound(target, "/Audio/Imperial/MartialArts/cqchit2.ogg");
        Disarm(target, user);
        Stamina(target, 50, user);
        Brute(target, 25, user);
        return true;
    }

    private MartialResult CqcGrab(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target, bool upgrade)
    {
        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "G", target);
        if (CqcCheckStreak(ent, target))
            return upgrade ? MartialResult.Success : MartialResult.SuccessKeepGrab;

        if (IsLying(ent))
            return MartialResult.Invalid;

        // Мгновенный агрессивный захват из пассивного.
        if (!upgrade || ent.Comp.GrabState == MartialGrabState.Passive)
        {
            DropAll(target);
            if (upgrade)
                SetGrabState(ent, target, MartialGrabState.Aggressive);
            else
                _pendingGrabState[ent.Owner] = MartialGrabState.Aggressive;

            PopupPair(ent, target, "martial-cqc-grab-others", "martial-cqc-grab-self", "martial-cqc-grab-target");
            return upgrade ? MartialResult.Success : MartialResult.SuccessKeepGrab;
        }

        UpgradeGrab(ent, target, instant: false);
        return MartialResult.Success;
    }

    private MartialResult CqcHarm(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (TrySnapNeck(ent, target))
            return MartialResult.Success;

        if (CheckBlock(target, ent, 10, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        // Подсечка из положения лёжа.
        if (IsLying(ent) && !_mobs.IsDead(target) && !IsLying(target))
        {
            PopupPair(ent, target, "martial-cqc-sweep-others", "martial-cqc-sweep-self", "martial-cqc-sweep-target");
            Sound(ent, "/Audio/Effects/hit_kick.ogg");
            Brute(target, 10, ent);
            Knockdown(target, TimeSpan.FromSeconds(5));
            ResetStreak(ent.Comp);
            return MartialResult.Success;
        }

        AddToStreak(ent, ent.Comp, art, "H", target);
        if (CqcCheckStreak(ent, target))
            return MartialResult.Success;

        var lying = IsLying(target);
        Brute(target, lying ? 18 : 13, ent);
        Sound(target, lying ? "/Audio/Imperial/MartialArts/cqchit2.ogg" : "/Audio/Imperial/MartialArts/cqchit1.ogg");

        var verb = lying
            ? _random.Pick(new[] { "kick", "stomp" })
            : _random.Pick(new[] { "cqc", "bigboss" });
        PopupPair(ent, target, $"martial-cqc-hit-{verb}-others", $"martial-cqc-hit-{verb}-self", $"martial-cqc-hit-{verb}-target");
        return MartialResult.Success;
    }

    private MartialResult CqcDisarm(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, "D", target);
        if (CqcCheckStreak(ent, target))
            return MartialResult.Success;

        // Удушающий приём по сдержанной цели.
        if (ent.Comp.GrabTarget == target && ent.Comp.Restraining == target)
        {
            PopupPair(ent, target, "martial-cqc-choke-others", "martial-cqc-choke-self", "martial-cqc-choke-target");
            Sleep(target, TimeSpan.FromSeconds(40));
            ent.Comp.Restraining = null;
            if (ent.Comp.GrabState < MartialGrabState.Neck)
                SetGrabState(ent, target, MartialGrabState.Neck);
            return MartialResult.Success;
        }

        if (_random.Prob(0.65f))
        {
            var item = Disarm(target, ent);
            PopupPair(ent, target,
                item != null ? "martial-cqc-jaw-disarm-others" : "martial-cqc-jaw-others",
                item != null ? "martial-cqc-jaw-disarm-self" : "martial-cqc-jaw-self",
                item != null ? "martial-cqc-jaw-disarm-target" : "martial-cqc-jaw-target");
            Sound(target, "/Audio/Imperial/MartialArts/cqchit1.ogg");
            Jitter(target, TimeSpan.FromSeconds(4));
            Brute(target, 5, ent);
            return MartialResult.Success;
        }

        PopupPair(ent, target, "martial-cqc-disarm-fail-others", "martial-cqc-disarm-fail-self", "martial-cqc-disarm-fail-target", PopupType.Small);
        Sound(target, "/Audio/Weapons/punchmiss.ogg");
        return MartialResult.Fail;
    }

    /// <summary>check_block(): в защитной стойке 75% блока, бьющего вплотную — заломить на 4 секунды.</summary>
    private bool CqcBlock(Entity<MartialArtsComponent> ent, EntityUid attacker, MartialAttackType type)
    {
        if (!ent.Comp.BlockMode || type == MartialAttackType.Projectile || !IsConscious(ent))
            return false;

        if (!_random.Prob(CqcBlockChance))
            return false;

        if (_xform.InRange(ent.Owner, attacker, MeleeRange))
        {
            PopupPair(ent, attacker, "martial-cqc-block-counter-others", "martial-cqc-block-self", "martial-cqc-block-counter-target");
            Stun(attacker, TimeSpan.FromSeconds(4));
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("martial-cqc-block-self", ("user", Identity(ent)), ("target", Identity(attacker))), ent, ent);
        }

        return true;
    }

    /// <summary>harm_act() на GRAB_KILL: свернуть шею одним движением.</summary>
    private bool TrySnapNeck(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (ent.Comp.GrabState != MartialGrabState.Kill || ent.Comp.GrabTarget != target || _mobs.IsDead(target))
            return false;

        Sound(target, "/Audio/Imperial/MartialArts/crack1.ogg", 5f);
        PopupPair(ent, target, "martial-neck-snap-others", "martial-neck-snap-self", "martial-neck-snap-target", PopupType.LargeCaution);
        Brute(target, 100, ent);
        _mobs.ChangeMobState(target, Content.Shared.Mobs.MobState.Dead, origin: ent);
        return true;
    }

    /// <summary>
    /// adjust_organ_loss(ORGAN_SLOT_BRAIN, …): у органов в сборке нет собственного урона,
    /// поэтому удар по мозгу не переносится — остаются нокдаун и потеря сознания.
    /// </summary>
    private void BrainDamage(EntityUid target, float amount)
    {
    }

    /// <summary>under_siege/can_use(): только на кухне.</summary>
    private bool IsInKitchen(EntityUid user)
    {
        var coords = Transform(user).Coordinates;
        foreach (var _ in _lookup.GetEntitiesInRange<MicrowaveComponent>(coords, KitchenRange))
        {
            return true;
        }

        return false;
    }
}
