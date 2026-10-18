using Robust.Shared.Random;
using System.Numerics;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MartialArts;

/// <summary>datum/martial_art/wrestling (wrestling.dm, Goonstation).</summary>
public sealed partial class MartialArtsSystem
{
    /// <summary>check_streak(): приём заряжен кнопкой и срабатывает следующей атакой.</summary>
    private bool WrestlingCheckStreak(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var move = ent.Comp.Streak;
        if (move is not ("drop" or "strike" or "kick" or "throw" or "slam"))
            return false;

        if (CheckBlock(target, ent, 10, MartialAttackType.Unarmed))
            return false;

        ent.Comp.Streak = string.Empty;
        switch (move)
        {
            case "drop":
                WrestlingDrop(ent, target);
                break;
            case "strike":
                WrestlingStrike(ent, target);
                break;
            case "kick":
                WrestlingKick(ent, target);
                break;
            case "throw":
                WrestlingThrow(ent, target);
                break;
            case "slam":
                WrestlingSlam(ent, target);
                break;
        }

        return true;
    }

    /// <summary>grab_act(): клинч — взять в захват и оглушить.</summary>
    private MartialResult WrestlingGrab(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (WrestlingCheckStreak(ent, target))
            return MartialResult.Success;

        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        if (ent.Comp.GrabTarget == target)
            return MartialResult.Fail;

        PopupPair(ent, target, "martial-wrestling-cinch-others", "martial-wrestling-cinch-self", "martial-wrestling-cinch-target");
        Stun(target, TimeSpan.FromSeconds(_random.NextFloat(6f, 10f)));
        return MartialResult.SuccessKeepGrab;
    }

    /// <summary>throw_wrassle(): раскрутить схваченного и швырнуть на десять клеток.</summary>
    private void WrestlingThrow(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (ent.Comp.GrabTarget != target)
        {
            _popup.PopupEntity(Loc.GetString("martial-wrestling-need-cinch", ("target", Identity(target))), ent, ent, PopupType.SmallCaution);
            return;
        }

        Stun(target, TimeSpan.FromSeconds(8));
        PopupPair(ent, target, "martial-wrestling-spin-others", "martial-wrestling-spin-self", "martial-wrestling-spin-target");
        Emote(ent, "Scream");

        // Двадцать оборотов с ускорением, как в оригинале.
        var delay = 0f;
        for (var i = 1; i <= 20; i++)
        {
            delay += i switch
            {
                >= 18 => 0.025f,
                >= 15 => 0.05f,
                >= 10 => 0.1f,
                >= 6 => 0.2f,
                _ => 0.3f,
            };

            var step = i;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () => WrestlingSpinStep(ent, target, step));
        }

        Timer.Spawn(TimeSpan.FromSeconds(delay + 0.1f), () => WrestlingThrowRelease(ent, target));
    }

    private void WrestlingSpinStep(EntityUid user, EntityUid target, int step)
    {
        if (!Exists(user) || !Exists(target) || !_xform.InRange(user, target, MeleeRange))
            return;

        var angle = Angle.FromDegrees(90 * step);
        var userPos = _xform.GetWorldPosition(user);
        _xform.SetWorldPosition(target, userPos + angle.ToWorldVec());
        _xform.SetWorldRotation(user, angle);
    }

    private void WrestlingThrowRelease(EntityUid user, EntityUid target)
    {
        if (!Exists(user) || !Exists(target))
            return;

        if (!_xform.InRange(user, target, MeleeRange * 1.5f))
        {
            _popup.PopupEntity(Loc.GetString("martial-wrestling-too-far", ("target", Identity(target))), user, user, PopupType.SmallCaution);
            return;
        }

        if (TryComp<MartialArtsComponent>(user, out var comp))
            StopGrab(user, comp);
        if (TryComp<Content.Shared.Movement.Pulling.Components.PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable);

        PopupPair(user, target, "martial-wrestling-throw-others", "martial-wrestling-throw-self", "martial-wrestling-throw-target");
        Sound(user, "/Audio/Weapons/genhit1.ogg");
        if (IsConscious(target))
            Emote(target, "Scream");

        ThrowAway(target, user, 10, 4);
        Timer.Spawn(TimeSpan.FromSeconds(1), () =>
        {
            if (Exists(target))
                Paralyze(target, TimeSpan.FromSeconds(2));
        });
    }

    /// <summary>slam(): поднять и впечатать — турбо- или атомный бросок.</summary>
    private void WrestlingSlam(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (ent.Comp.GrabTarget != target)
        {
            _popup.PopupEntity(Loc.GetString("martial-wrestling-need-cinch", ("target", Identity(target))), ent, ent, PopupType.SmallCaution);
            return;
        }

        _xform.SetCoordinates(target, Transform(ent).Coordinates);
        PopupPair(ent, target, "martial-wrestling-lift-others", "martial-wrestling-lift-self", "martial-wrestling-lift-target");

        Timer.Spawn(TimeSpan.FromSeconds(0.3), () =>
        {
            if (!Exists(ent) || !Exists(target) || !_xform.InRange(ent.Owner, target, MeleeRange))
                return;

            var fluff = _random.Prob(0.5f) ? "turbo" : "atomic";
            PopupPair(ent, target, $"martial-wrestling-slam-{fluff}-others", $"martial-wrestling-slam-{fluff}-self", $"martial-wrestling-slam-{fluff}-target");
            Sound(ent, "/Audio/Weapons/genhit1.ogg");

            if (IsConscious(target))
            {
                Emote(target, "Scream");
                Paralyze(target, TimeSpan.FromSeconds(4));
                switch (_random.Next(1, 4))
                {
                    case 2:
                        Brute(target, _random.Next(20, 31), ent);
                        break;
                    case 3:
                        ExplodeLight(target, ent);
                        break;
                    default:
                        Brute(target, _random.Next(10, 21), ent);
                        break;
                }
            }
            else
            {
                ExplodeLight(target, ent);
            }
        });
    }

    /// <summary>strike(): удар головой с рывком на клетку цели.</summary>
    private void WrestlingStrike(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var origin = Transform(ent).Coordinates;
        _xform.SetCoordinates(ent, Transform(target).Coordinates);
        Timer.Spawn(TimeSpan.FromSeconds(0.4), () =>
        {
            if (Exists(ent))
                _xform.SetCoordinates(ent, origin);
        });

        PopupPair(ent, target, "martial-wrestling-headbutt-others", "martial-wrestling-headbutt-self", "martial-wrestling-headbutt-target");
        Brute(target, _random.Next(10, 21), ent);
        Sound(ent, "/Audio/Weapons/genhit1.ogg");
        Sleep(target, TimeSpan.FromSeconds(2));
    }

    /// <summary>kick(): удар с разворота отбрасывает на три клетки.</summary>
    private void WrestlingKick(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        Emote(ent, "Scream");
        PopupPair(ent, target, "martial-wrestling-roundhouse-others", "martial-wrestling-roundhouse-self", "martial-wrestling-roundhouse-target");
        Sound(ent, "/Audio/Weapons/genhit1.ogg");
        Brute(target, _random.Next(10, 21), ent);
        Paralyze(target, TimeSpan.FromSeconds(2));
        ThrowAway(target, ent, 3, 2);
    }

    /// <summary>drop(): прыжок с ближайшего возвышения ногами на противника.</summary>
    private void WrestlingDrop(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var falling = HasClimbableNearby(ent);
        if (falling)
            PopupPair(ent, ent, "martial-wrestling-climb-others", "martial-wrestling-climb-self");

        Timer.Spawn(TimeSpan.FromSeconds(falling ? 2 : 1), () =>
        {
            if (!Exists(ent) || !Exists(target))
                return;

            if (!_xform.InRange(ent.Owner, target, falling ? MeleeRange * 2 : MeleeRange))
            {
                if (falling)
                {
                    PopupPair(ent, ent, "martial-wrestling-dive-fail-others", "martial-wrestling-dive-fail-self");
                    Brute(ent, _random.Next(10, 21), null);
                    Paralyze(ent, TimeSpan.FromSeconds(6));
                }

                _popup.PopupEntity(Loc.GetString("martial-wrestling-too-far", ("target", Identity(target))), ent, ent, PopupType.SmallCaution);
                return;
            }

            _xform.SetCoordinates(ent, Transform(target).Coordinates);
            PopupPair(ent, target, "martial-wrestling-legdrop-others", "martial-wrestling-legdrop-self", "martial-wrestling-legdrop-target");
            Sound(ent, "/Audio/Weapons/genhit1.ogg");
            Emote(ent, "Scream");

            if (falling && (_random.Prob(0.33f) || !IsConscious(target)))
                ExplodeLight(target, ent);
            else
                Brute(target, _random.Next(20, 31), ent);

            Paralyze(target, TimeSpan.FromSeconds(4));
        });
    }

    /// <summary>oview(1): есть ли рядом плотный, но прозрачный объект, на который можно залезть.</summary>
    private bool HasClimbableNearby(EntityUid user)
    {
        foreach (var ent in _lookup.GetEntitiesInRange(Transform(user).Coordinates, 1.5f))
        {
            if (ent == user || HasComp<Content.Shared.Mobs.Components.MobStateComponent>(ent))
                continue;

            if (HasComp<Content.Shared.Climbing.Components.ClimbableComponent>(ent))
                return true;
        }

        return false;
    }

    /// <summary>EX_ACT(defender, EXPLODE_LIGHT): лёгкий взрыв только по одной цели.</summary>
    private void ExplodeLight(EntityUid target, EntityUid origin)
    {
        Brute(target, 30, origin);
        Sound(target, "/Audio/Effects/explosion1.ogg", -4f);
    }
}
