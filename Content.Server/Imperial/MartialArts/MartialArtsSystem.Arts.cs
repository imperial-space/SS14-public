using Content.Shared.Imperial.MartialArts;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server.Imperial.MartialArts;

/// <summary>Распределение harm/disarm/grab/help по искусствам и их пассивные свойства.</summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    private void InitializeArts()
    {
        SubscribeLocalEvent<MartialArtsComponent, ShotAttemptedEvent>(OnShotAttempted);
        InitializeCarp();
        InitializeMush();
    }

    /// <summary>can_use().</summary>
    private bool CanUse(EntityUid user, MartialArtPrototype art)
    {
        return art.Kind switch
        {
            MartialArtKind.CloseQuartersCooking => IsInKitchen(user),
            MartialArtKind.Boxing or MartialArtKind.EvilBoxing or MartialArtKind.HunterBoxing => IsHuman(user),
            _ => true,
        };
    }

    private MartialResult HarmAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        return art.Kind switch
        {
            MartialArtKind.Cqc or MartialArtKind.CloseQuartersCooking => CqcHarm(ent, art, target),
            MartialArtKind.SleepingCarp => CarpHarm(ent, art, target),
            MartialArtKind.Boxing or MartialArtKind.EvilBoxing or MartialArtKind.HunterBoxing => BoxingAct(ent, art, target, 'H'),
            MartialArtKind.Wrestling => WrestlingCheckStreak(ent, target) ? MartialResult.Success : MartialResult.Invalid,
            MartialArtKind.PlasmaFist or MartialArtKind.PlasmaFistNoBomb => PlasmaAct(ent, art, target, 'H'),
            MartialArtKind.PsychoticBrawling => PsychoAttack(ent, target, grab: false),
            MartialArtKind.MushroomPunch => MushHarm(ent, target),
            MartialArtKind.JungleArts => JungleAttack(ent, target),
            MartialArtKind.SpidersBite => SpiderPunch(ent, target),
            MartialArtKind.KazaRuk => KazaHarm(ent, target),
            MartialArtKind.CorporateJudo => JudoHarm(ent, art, target),
            _ => MartialResult.Invalid,
        };
    }

    private MartialResult DisarmAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        return art.Kind switch
        {
            MartialArtKind.Cqc or MartialArtKind.CloseQuartersCooking => CqcDisarm(ent, art, target),
            MartialArtKind.SleepingCarp => CarpDisarm(ent, art, target),
            MartialArtKind.Boxing or MartialArtKind.EvilBoxing or MartialArtKind.HunterBoxing => BoxingAct(ent, art, target, 'D'),
            MartialArtKind.Wrestling => WrestlingCheckStreak(ent, target) ? MartialResult.Success : MartialResult.Invalid,
            MartialArtKind.PlasmaFist or MartialArtKind.PlasmaFistNoBomb => PlasmaAct(ent, art, target, 'D'),
            MartialArtKind.PsychoticBrawling => PsychoAttack(ent, target, grab: false),
            MartialArtKind.JungleArts => JungleAttack(ent, target),
            MartialArtKind.KazaRuk => KazaDisarm(ent, target),
            MartialArtKind.CorporateJudo => JudoDisarm(ent, art, target),
            _ => MartialResult.Invalid,
        };
    }

    private MartialResult GrabAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target, bool upgrade)
    {
        return art.Kind switch
        {
            MartialArtKind.Cqc or MartialArtKind.CloseQuartersCooking => CqcGrab(ent, art, target, upgrade),
            MartialArtKind.SleepingCarp => CarpGrab(ent, art, target),
            MartialArtKind.Boxing => BoxingGrab(ent),
            MartialArtKind.Wrestling => WrestlingGrab(ent, target),
            MartialArtKind.PlasmaFist or MartialArtKind.PlasmaFistNoBomb => PlasmaAct(ent, art, target, 'G'),
            MartialArtKind.PsychoticBrawling => PsychoAttack(ent, target, grab: true),
            MartialArtKind.JungleArts => JungleAttack(ent, target),
            MartialArtKind.CorporateJudo => JudoGrab(ent, art, target),
            _ => MartialResult.Invalid,
        };
    }

    private void HelpAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid target)
    {
        if (art.Kind == MartialArtKind.CorporateJudo)
            JudoHelp(ent, art, target);
    }

    /// <summary>COMSIG_LIVING_CHECK_BLOCK у искусств, которые умеют защищаться.</summary>
    private bool BlockAct(Entity<MartialArtsComponent> ent, MartialArtPrototype art, EntityUid attacker, float damage, MartialAttackType type)
    {
        return art.Kind switch
        {
            MartialArtKind.Cqc or MartialArtKind.CloseQuartersCooking => CqcBlock(ent, attacker, type),
            MartialArtKind.SleepingCarp => CarpDodge(ent, type),
            MartialArtKind.Boxing or MartialArtKind.EvilBoxing or MartialArtKind.HunterBoxing => BoxingBlock(ent, art, attacker, damage, type),
            _ => false,
        };
    }

    /// <summary>Приёмы с кнопок: борьба, Каза Рук, дзюдо заряжают следующую атаку.</summary>
    private void OnMoveAction(Entity<MartialArtsComponent> ent, ref MartialArtsMoveActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // Повторное нажатие той же стойки — вернуться в нейтральную (Каза Рук, дзюдо).
        if (ent.Comp.Streak == args.Move && GetActive(ent.Comp) is { Kind: not MartialArtKind.Wrestling })
        {
            ent.Comp.Streak = string.Empty;
            _popup.PopupEntity(Loc.GetString("martial-stance-neutral-others", ("user", Identity(ent))), ent, Robust.Shared.Player.Filter.PvsExcept(ent), true);
            _popup.PopupEntity(Loc.GetString("martial-stance-neutral-self"), ent, ent);
            return;
        }

        ent.Comp.Streak = args.Move;
        ent.Comp.StreakTarget = null;
        var move = Loc.GetString($"martial-move-{args.Move}");
        _popup.PopupEntity(Loc.GetString("martial-stance-others", ("user", Identity(ent)), ("move", move)), ent, Robust.Shared.Player.Filter.PvsExcept(ent), true, Content.Shared.Popups.PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("martial-stance-self", ("move", move)), ent, ent);
    }

    private void OnStyleActivated(EntityUid user, MartialArtsComponent comp, MartialArtPrototype art)
    {
        switch (art.Kind)
        {
            case MartialArtKind.SleepingCarp:
                // add_faction(FACTION_CARP): карпы своих не трогают.
                _faction.AddFaction(user, CarpFaction);
                break;
            case MartialArtKind.CorporateJudo:
                JudoDropBatons(user);
                break;
        }
    }

    private void OnStyleDeactivated(EntityUid user, MartialArtsComponent comp, MartialArtPrototype art)
    {
        if (art.Kind == MartialArtKind.SleepingCarp && !TerminatingOrDeleted(user))
            _faction.RemoveFaction(user, CarpFaction);
    }

    /// <summary>TRAIT_NOGUNS: Спящий карп отказывается от стрелкового оружия.</summary>
    private void OnShotAttempted(Entity<MartialArtsComponent> ent, ref ShotAttemptedEvent args)
    {
        if (GetActive(ent.Comp) is not { Kind: MartialArtKind.SleepingCarp })
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("martial-carp-no-guns"), ent, ent, Content.Shared.Popups.PopupType.SmallCaution);
    }
}
