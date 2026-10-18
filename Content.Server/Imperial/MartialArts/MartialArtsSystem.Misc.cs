using Robust.Shared.Random;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Mind;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Плазменный кулак, психобрань, грибной удар, искусство джунглей и укус паука
/// (plasma_fist.dm, psychotic_brawl.dm, mushpunch.dm, junglearts.dm, spiders_bite.dm).
/// </summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly SharedMindSystem _sharedMind = default!;

    private const string PlasmaTornado = "HHD";
    private const string PlasmaThrowback = "DHD";
    private const string PlasmaFist = "HDDDH";

    /// <summary>plasma_increment и plasma_cap.</summary>
    private const int PlasmaIncrement = 1;
    private const int PlasmaCap = 12;

    /// <summary>aoe_radius заклинания repulse, которое кастует «Торнадо».</summary>
    private const float RepulseRange = 5f;

    private void InitializeMush()
    {
        SubscribeLocalEvent<MartialArtsComponent, MushroomPunchDoAfterEvent>(OnMushPunchDoAfter);
    }

    #region Плазменный кулак

    private MartialResult PlasmaAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target, char button)
    {
        if (CheckBlock(target, ent, button == 'H' ? 10 : 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        AddToStreak(ent, ent.Comp, art, button.ToString(), target);
        return PlasmaCheckStreak(ent, art, target) ? MartialResult.Success : MartialResult.Invalid;
    }

    private bool PlasmaCheckStreak(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        var streak = ent.Comp.Streak;
        if (streak.Contains(PlasmaTornado))
        {
            ResetStreak(ent.Comp);
            PlasmaTornadoMove(ent);
            return true;
        }

        if (streak.Contains(PlasmaThrowback))
        {
            ResetStreak(ent.Comp);
            PopupPair(ent, target, "martial-plasma-punch-others", "martial-plasma-punch-self", "martial-plasma-punch-target");
            Sound(target, "/Audio/Weapons/punch1.ogg");
            ThrowAway(target, ent, 200, 4);
            Say(ent, Loc.GetString("martial-plasma-hyah"));
            return true;
        }

        if (streak.Contains(PlasmaFist))
        {
            ResetStreak(ent.Comp);
            PlasmaFistMove(ent, art, target);
            return true;
        }

        return false;
    }

    /// <summary>Tornado(): TORNADO SWEEP! — всех вокруг отбрасывает волной.</summary>
    private void PlasmaTornadoMove(EntityUid user)
    {
        Say(user, Loc.GetString("martial-plasma-tornado"));
        Sound(user, "/Audio/Weapons/punch1.ogg", -8f);

        foreach (var ent in _lookup.GetEntitiesInRange<PhysicsComponent>(Transform(user).Coordinates, RepulseRange))
        {
            if (ent.Owner == user || Transform(ent).Anchored)
                continue;

            var distance = (_xform.GetWorldPosition(ent) - _xform.GetWorldPosition(user)).Length();
            ThrowAway(ent, user, Math.Max(1f, RepulseRange - distance + 1f), 4);
            if (HasComp<Content.Shared.Mobs.Components.MobStateComponent>(ent) && distance < 1.5f)
                Paralyze(ent, TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Plasma(): THE PLASMA FIST TECHNIQUE — разорвать противника на куски.</summary>
    private void PlasmaFistMove(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        var noBomb = art.Kind == MartialArtKind.PlasmaFistNoBomb;

        // По себе — Апофеоз.
        if (target == ent.Owner)
        {
            if (!noBomb)
                PlasmaApotheosis(ent);
            return;
        }

        var hadClient = _sharedMind.TryGetMind(target, out _, out var mind) && mind.UserId != null;
        Sound(target, "/Audio/Weapons/punch1.ogg");
        Say(ent, Loc.GetString("martial-plasma-fist"));
        PopupPair(ent, target, "martial-plasma-fist-others", "martial-plasma-fist-self", "martial-plasma-fist-target", PopupType.LargeCaution);
        _gibbing.Gib(target);

        if (noBomb)
            return;

        if (!hadClient)
            _popup.PopupEntity(Loc.GetString("martial-plasma-dishonor"), ent, ent, PopupType.SmallCaution);
        else if (ent.Comp.PlasmaPower >= PlasmaCap)
            _popup.PopupEntity(Loc.GetString("martial-plasma-capped"), ent, ent, PopupType.SmallCaution);
        else
        {
            ent.Comp.PlasmaPower += PlasmaIncrement;
            _popup.PopupEntity(Loc.GetString("martial-plasma-power", ("power", ent.Comp.PlasmaPower)), ent, ent, PopupType.Medium);
        }
    }

    /// <summary>Apotheosis(): АПОФЕОЗ!! — взрыв силой plasma_power, plasma_power*2, plasma_power*4.</summary>
    private void PlasmaApotheosis(Entity<MartialArtsComponent> ent)
    {
        Say(ent, Loc.GetString("martial-plasma-apotheosis"));
        _popup.PopupEntity(Loc.GetString("martial-plasma-soul-out"), ent, ent, PopupType.LargeCaution);

        var power = ent.Comp.PlasmaPower;
        ent.Comp.PlasmaPower = 1;

        var coords = _xform.GetMapCoordinates(ent);
        Brute(ent, _random.Next(50, 71), null);

        // Девастация power, тяжёлый урон power*2, лёгкий power*4 — по площади это примерно 4*power тайлов.
        var intensity = 30f * power * power;
        _explosion.QueueExplosion(coords, ExplosionSystem.DefaultExplosionPrototypeId, intensity, 4f, 10f * power, ent.Owner, canCreateVacuum: true);

        // ghostize(FALSE): душа вылетает из тела, а через 6 секунд тело умирает.
        if (_sharedMind.TryGetMind(ent, out var mindId, out var mind))
            _sharedMind.WipeMind(mindId, mind);

        Timer.Spawn(TimeSpan.FromSeconds(6), () =>
        {
            if (Exists(ent) && !_mobs.IsDead(ent))
                _mobs.ChangeMobState(ent, Content.Shared.Mobs.MobState.Dead);
        });
    }

    #endregion

    #region Психобрань

    /// <summary>psycho_attack(): что угодно из восьми исходов.</summary>
    private MartialResult PsychoAttack(Entity<MartialArtsComponent> ent, EntityUid target, bool grab)
    {
        switch (_random.Next(1, 9))
        {
            case 1:
                // help_shake_act(): попытаться растормошить.
                PopupPair(ent, target, "martial-psycho-help-others", "martial-psycho-help-self", "martial-psycho-help-target", PopupType.Small);
                return MartialResult.Success;
            case 2:
                Emote(ent, "Crying");
                Stun(ent, TimeSpan.FromSeconds(2));
                PopupPair(ent, target, "martial-psycho-cry-others", "martial-psycho-cry-self", null, PopupType.Small);
                return MartialResult.Success;
            case 3:
                if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
                    return MartialResult.Fail;
                if (IsLying(ent))
                    return MartialResult.Invalid;

                if (ent.Comp.GrabTarget == target && ent.Comp.GrabState >= MartialGrabState.Aggressive)
                {
                    UpgradeGrab(ent, target, instant: true);
                }
                else if (StartGrab(ent, target, grab ? MartialGrabState.Aggressive : MartialGrabState.Passive))
                {
                    DropAll(target);
                    if (TryComp<PullerComponent>(target, out var puller) && puller.Pulling is { } pulled && TryComp<PullableComponent>(pulled, out var pullable))
                        _pulling.TryStopPull(pulled, pullable);
                    if (grab)
                        PopupPair(ent, target, "martial-psycho-grab-others", "martial-psycho-grab-self", "martial-psycho-grab-target");
                }

                return MartialResult.Fail;
            case 4:
            {
                var damage = _random.Next(5, 11);
                if (CheckBlock(target, ent, damage, MartialAttackType.Unarmed))
                    return MartialResult.Fail;

                PopupPair(ent, target, "martial-psycho-headbutt-others", "martial-psycho-headbutt-self", "martial-psycho-headbutt-target");
                Sound(target, "/Audio/Weapons/punch1.ogg", -4f);
                Brute(target, damage, ent);
                Brute(ent, _random.Next(5, 11), null);
                Stun(ent, TimeSpan.FromSeconds(_random.NextFloat(1f, 4.5f)));
                Stun(target, TimeSpan.FromSeconds(_random.NextFloat(0.5f, 3f)));
                return MartialResult.Success;
            }
            case 5:
            case 6:
                if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
                    return MartialResult.Fail;

                PopupPair(ent, target, "martial-psycho-fling-others", "martial-psycho-fling-self", "martial-psycho-fling-target");
                Brute(target, _random.Next(15, 31), ent);
                Sound(target, "/Audio/Imperial/MartialArts/meteorimpact.ogg", -6f);
                ThrowAway(target, ent, 4, 2);
                Paralyze(target, TimeSpan.FromSeconds(6));
                return MartialResult.Success;
            default:
                return MartialResult.Invalid;
        }
    }

    #endregion

    #region Грибной удар

    /// <summary>harm_act(): удар нужно зарядить за 2.5 секунды.</summary>
    private MartialResult MushHarm(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("martial-mush-windup"), ent, ent, PopupType.Medium);
        var doAfter = new DoAfterArgs(EntityManager, ent, TimeSpan.FromSeconds(2.5), new MushroomPunchDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        return MartialResult.Success;
    }

    private void OnMushPunchDoAfter(Entity<MartialArtsComponent> ent, ref MushroomPunchDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("martial-mush-interrupted"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var damage = _random.Next(15, 31);
        if (CheckBlock(target, ent, damage, MartialAttackType.Unarmed))
            return;

        PopupPair(ent, target, "martial-mush-hit-others", "martial-mush-hit-self", "martial-mush-hit-target");
        Brute(target, damage, ent);
        Sound(target, "/Audio/Imperial/MartialArts/meteorimpact.ogg", -6f);
        ThrowAway(target, ent, 4, 2);
        Paralyze(target, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region Искусство джунглей

    /// <summary>jungle_attack(): хвостом сбить с ног, хвостом хлестнуть или укусить.</summary>
    private MartialResult JungleAttack(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        var roll = _random.Next(1, 7);
        var hasTail = HasTail(ent);

        if (roll == 1)
        {
            if (!hasTail && !IsConscious(target) || HasComp<Content.Shared.Stunnable.StunnedComponent>(target))
                return MartialResult.Invalid;

            PopupPair(ent, target, "martial-jungle-drag-others", "martial-jungle-drag-self", "martial-jungle-drag-target");
            Brute(target, _random.Next(5, 11), ent);
            Sound(ent, "/Audio/Imperial/MartialArts/whip.ogg");
            Knockdown(target, TimeSpan.FromSeconds(2));
            return MartialResult.Success;
        }

        if (roll == 6)
        {
            if (!hasTail)
                return MartialResult.Invalid;

            PopupPair(ent, target, "martial-jungle-whip-others", "martial-jungle-whip-self", "martial-jungle-whip-target");
            Brute(target, _random.Next(10, 16), ent);
            Sound(ent, "/Audio/Imperial/MartialArts/whip.ogg");
            DropAll(target);
            return MartialResult.Success;
        }

        if (CheckBlock(target, ent, 0, MartialAttackType.Unarmed))
            return MartialResult.Fail;

        PopupPair(ent, target, "martial-jungle-bite-others", "martial-jungle-bite-self", "martial-jungle-bite-target");
        Brute(target, _random.Next(10, 21), ent, "Piercing");
        Sound(ent, "/Audio/Effects/bite.ogg");
        return MartialResult.Success;
    }

    /// <summary>ORGAN_SLOT_EXTERNAL_TAIL: хвост есть у ящеров, вульп, фелинидов и обезьян.</summary>
    private bool HasTail(EntityUid uid)
    {
        if (!TryComp<Content.Shared.Humanoid.HumanoidProfileComponent>(uid, out var humanoid))
            return true; // обезьяны и прочие звери

        var species = humanoid.Species.Id;
        return species is "Reptilian" or "Vulpkanin" or "Felinid" or "Tajaran";
    }

    #endregion

    #region Укус паука

    /// <summary>
    /// Many Legged Spider: по ошеломлённым — только пинки, Flow of Gravity: пинок с шансом выбивает оружие.
    /// Сам удар обычный, поэтому возвращается Invalid.
    /// </summary>
    private MartialResult SpiderPunch(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (!IsStaggered(target))
        {
            ent.Comp.SpiderLastHit = null;
            return MartialResult.Invalid;
        }

        if (ent.Comp.SpiderLastHit == target)
            ent.Comp.SpiderLastHitCount++;
        else
        {
            ent.Comp.SpiderLastHit = target;
            ent.Comp.SpiderLastHitCount = 1;
        }

        // Пинок бьёт сильнее кулака: урон ноги вместо руки.
        Brute(target, _random.Next(2, 6), ent);

        if (!_random.Prob(Math.Min(1f, 0.33f * ent.Comp.SpiderLastHitCount)))
            return MartialResult.Invalid;

        if (Disarm(target, null) is { } weapon)
            PopupPair(ent, target, "martial-spider-kick-disarm-others", "martial-spider-kick-disarm-self", "martial-spider-kick-disarm-target");

        return MartialResult.Invalid;
    }

    #endregion
}
