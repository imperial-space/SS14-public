using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Imperial.BloodBrother;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Flash;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

/// <summary>
/// Братья по крови 1 в 1 с BandaStation (code/modules/antagonists/brother/brother.dm):
/// команда с общими целями, вербовка вспышкой и общий счётчик пробуждений.
/// </summary>
public sealed class BloodBrotherRuleSystem : GameRuleSystem<BloodBrotherRuleComponent>
{
    private static readonly Color BriefingColor = Color.FromHex("#8b1e1e");
    private const string DefaultBloodBrotherRule = "BloodBrother";

    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjective = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
        SubscribeLocalEvent<BloodBrotherConverterComponent, AfterFlashedEvent>(OnAfterFlashed);
        SubscribeLocalEvent<BloodBrotherConvertConditionComponent, ObjectiveGetProgressEvent>(OnConvertProgress);
    }

    #region Создание

    /// <summary>Сделать двух игроков братьями по крови. Используется командой и окном выбора.</summary>
    public bool TryMakeBloodBrotherPair(ICommonSession first, ICommonSession second)
    {
        if (first == second)
            return false;

        var rule = _antag.ForceGetGameRuleEnt<BloodBrotherRuleComponent>(DefaultBloodBrotherRule);
        if (!_antag.TryGetNextAvailableDefinition(rule, out var definition))
            definition = rule.Comp.Definitions[^1];

        _antag.MakeAntag(rule, first, definition.Value);
        _antag.MakeAntag(rule, second, definition.Value);
        return true;
    }

    /// <summary>Сделать братом по крови одного игрока: напарника он пробудит сам.</summary>
    public bool TryMakeSoloBloodBrother(ICommonSession player)
    {
        var rule = _antag.ForceGetGameRuleEnt<BloodBrotherRuleComponent>(DefaultBloodBrotherRule);
        if (!_antag.TryGetNextAvailableDefinition(rule, out var definition))
            definition = rule.Comp.Definitions[^1];

        _antag.MakeAntag(rule, player, definition.Value);
        return true;
    }

    private void OnAfterAntagSelected(Entity<BloodBrotherRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out _))
            return;

        var team = EnsureTeam(ent);

        // team/New(): с небольшим шансом Синдикат оплачивает лишнего брата.
        if (!team.Comp.ObjectivesForged && _random.Prob(team.Comp.ExtraBrotherChance))
            team.Comp.BrothersLeft++;

        AddBrother(team, mindId, announce: false);
    }

    private Entity<BloodBrotherTeamComponent> EnsureTeam(Entity<BloodBrotherRuleComponent> rule)
    {
        return (rule.Owner, EnsureComp<BloodBrotherTeamComponent>(rule));
    }

    /// <summary>team/add_brother(): принять разум в команду, выдать цели, снаряжение и вербовку.</summary>
    public bool AddBrother(Entity<BloodBrotherTeamComponent> team, EntityUid mindId, bool announce = true)
    {
        if (team.Comp.Members.Contains(mindId) || HasComp<BloodBrotherComponent>(mindId))
            return false;

        team.Comp.Members.Add(mindId);
        var brother = EnsureComp<BloodBrotherComponent>(mindId);
        brother.Team = team;

        // Роль нужна раньше целей: у целей стоит требование роли.
        if (!_roles.MindHasRole<BloodBrotherRoleComponent>(mindId))
            _roles.MindAddRole(mindId, "MindRoleBloodBrother", silent: true);

        // Цели куются один раз на команду, от имени первого брата.
        if (!team.Comp.ObjectivesForged)
            ForgeObjectives(team, mindId);

        SetBrothersLeft(team, team.Comp.BrothersLeft - 1);

        // Общие цели команды висят у всех братьев разом.
        GiveTeamObjectives(team, mindId);

        UpdateTeamName(team);
        Greet(team, mindId);

        if (announce)
            AnnounceNewBrother(team, mindId);

        // on_gain(): вспышка и вербовка достаются только тем, кому есть кого будить.
        if (team.Comp.BrothersLeft > 0)
        {
            GrantConversion(team, mindId);
            GiveFlash(team, mindId);

            if (team.Comp.Members.Count > 1)
                SendMessage(mindId, "blood-brother-high-hopes");
        }

        return true;
    }

    private void GiveTeamObjectives(Entity<BloodBrotherTeamComponent> team, EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return;

        foreach (var objective in team.Comp.Objectives)
        {
            if (!mind.Objectives.Contains(objective))
                _mind.AddObjective(mindId, mind, objective);
        }
    }

    /// <summary>set_brothers_left(): счётчик управляет тем, кто умеет вербовать.</summary>
    private void SetBrothersLeft(Entity<BloodBrotherTeamComponent> team, int remaining)
    {
        if (team.Comp.BrothersLeft == remaining)
            return;

        var wasZero = team.Comp.BrothersLeft <= 0;
        team.Comp.BrothersLeft = remaining;

        if (wasZero && remaining > 0)
        {
            foreach (var mindId in team.Comp.Members)
            {
                GrantConversion(team, mindId);
            }
        }
        else if (!wasZero && remaining <= 0)
        {
            foreach (var mindId in team.Comp.Members)
            {
                RemoveConversion(mindId);
            }
        }
    }

    private void GrantConversion(Entity<BloodBrotherTeamComponent> team, EntityUid mindId)
    {
        if (CompOrNull<MindComponent>(mindId)?.OwnedEntity is not { } body)
            return;

        EnsureComp<BloodBrotherConverterComponent>(body).Team = team;
    }

    private void RemoveConversion(EntityUid mindId)
    {
        if (CompOrNull<MindComponent>(mindId)?.OwnedEntity is { } body)
            RemComp<BloodBrotherConverterComponent>(body);
    }

    /// <summary>equip_conspicuous_item(flash): вспышка для пробуждения следующего брата.</summary>
    private void GiveFlash(Entity<BloodBrotherTeamComponent> team, EntityUid mindId)
    {
        if (CompOrNull<MindComponent>(mindId)?.OwnedEntity is not { } body)
            return;

        var flash = Spawn(team.Comp.FlashProto, _transform.GetMoverCoordinates(body));
        if (!_hands.TryPickupAnyHand(body, flash))
            _transform.SetCoordinates(flash, _transform.GetMoverCoordinates(body));
    }

    #endregion

    #region Вербовка

    /// <summary>on_mob_successful_flashed_mob(): вспышка в упор пробуждает нового брата.</summary>
    private void OnAfterFlashed(Entity<BloodBrotherConverterComponent> ent, ref AfterFlashedEvent args)
    {
        // COMSIG_MOB_SUCCESSFUL_FLASH_ATTACK: пробуждают только вспышкой в упор,
        // площадной засвет брата не делает.
        var target = args.Target;
        if (!args.Melee || args.User != ent.Owner || target == ent.Owner)
            return;

        if (!TryComp<BloodBrotherTeamComponent>(ent.Comp.Team, out var teamComp))
            return;

        var team = new Entity<BloodBrotherTeamComponent>(ent.Comp.Team, teamComp);

        // Силиконы и прочие нелюди не годятся.
        if (!HasComp<HumanoidProfileComponent>(target) || _mobState.IsDead(target))
            return;

        if (!_mobState.IsAlive(target))
        {
            Deny(ent, target, "blood-brother-convert-unconscious");
            return;
        }

        if (!_mind.TryGetMind(target, out var targetMind, out var targetMindComp) || targetMindComp.UserId == null)
        {
            Deny(ent, target, "blood-brother-convert-no-mind");
            return;
        }

        // Цель задания будить нельзя.
        if (IsObjectiveTarget(team, targetMind))
        {
            Deny(ent, target, "blood-brother-convert-is-target");
            return;
        }

        if (HasComp<BloodBrotherComponent>(targetMind))
        {
            Deny(ent, target, "blood-brother-convert-already-loyal");
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            Deny(ent, target, "blood-brother-convert-resists");
            return;
        }

        if (teamComp.BrothersLeft <= 0 || !AddBrother(team, targetMind))
        {
            Deny(ent, target, "blood-brother-convert-failed");
            return;
        }

        // Вспышка перегорает от такой работы.
        if (args.Used is { } flash)
            QueueDel(flash);

        _popup.PopupEntity(Loc.GetString("blood-brother-convert-success"), target, ent.Owner, PopupType.Medium);
    }

    private void Deny(EntityUid user, EntityUid target, LocId message)
    {
        _popup.PopupEntity(Loc.GetString(message, ("target", Name(target))), target, user, PopupType.SmallCaution);
    }

    private bool IsObjectiveTarget(Entity<BloodBrotherTeamComponent> team, EntityUid targetMind)
    {
        foreach (var objective in team.Comp.Objectives)
        {
            if (_targetObjective.GetTarget(objective, out var target) && target == targetMind)
                return true;
        }

        return false;
    }

    #endregion

    #region Цели

    /// <summary>forge_brother_objectives().</summary>
    private void ForgeObjectives(Entity<BloodBrotherTeamComponent> team, EntityUid owner)
    {
        team.Comp.ObjectivesForged = true;

        AddTeamObjective(team, owner, team.Comp.ConvertObjective);

        var hijacker = _random.Prob(team.Comp.HijackChance);
        var count = Math.Max(1, team.Comp.BaseObjectiveAmount + (team.Comp.BrothersLeft > 2 ? 1 : 0) - (hijacker ? 1 : 0));
        for (var i = 0; i < count; i++)
        {
            ForgeSingleObjective(team, owner);
        }

        AddTeamObjective(team, owner, hijacker ? team.Comp.HijackObjective : team.Comp.EscapeObjective);
    }

    /// <summary>forge_single_objective().</summary>
    private void ForgeSingleObjective(Entity<BloodBrotherTeamComponent> team, EntityUid owner)
    {
        if (_random.Prob(0.5f))
        {
            // Чем меньше народу на станции, тем вероятнее заказ на ИИ.
            var players = Math.Max(1, _players.PlayerCount);
            if (HasStationAi() && _random.Prob(Math.Clamp(1f / players, 0f, 1f)))
            {
                AddTeamObjective(team, owner, team.Comp.DestroyAiObjective);
                return;
            }

            AddTeamObjective(team, owner, _random.Prob(0.3f) ? team.Comp.MaroonObjective : team.Comp.AssassinateObjective);
            return;
        }

        if (TryComp<MindComponent>(owner, out var mind)
            && _objectives.GetRandomObjective(owner, mind, team.Comp.StealObjectiveGroup, float.MaxValue) is { } steal)
        {
            RegisterObjective(team, steal);
        }
    }

    private bool HasStationAi()
    {
        var query = EntityQueryEnumerator<StationAiHeldComponent>();
        return query.MoveNext(out _, out _);
    }

    private void AddTeamObjective(Entity<BloodBrotherTeamComponent> team, EntityUid owner, EntProtoId proto)
    {
        if (!TryComp<MindComponent>(owner, out var mind))
            return;

        if (!_objectives.TryCreateObjective((owner, mind), proto, out var objective))
            return;

        RegisterObjective(team, objective.Value);
    }

    private void RegisterObjective(Entity<BloodBrotherTeamComponent> team, EntityUid objective)
    {
        if (team.Comp.Objectives.Contains(objective))
            return;

        team.Comp.Objectives.Add(objective);

        // Цель создавалась от имени первого брата, но принадлежит всей команде.
        foreach (var mindId in team.Comp.Members)
        {
            if (TryComp<MindComponent>(mindId, out var mind) && !mind.Objectives.Contains(objective))
                _mind.AddObjective(mindId, mind, objective);
        }
    }

    /// <summary>objective/convert_brother/check_completion(): в команде должен быть не один брат.</summary>
    private void OnConvertProgress(Entity<BloodBrotherConvertConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQueryEnumerator<BloodBrotherTeamComponent>();
        while (query.MoveNext(out _, out var team))
        {
            if (!team.Objectives.Contains(ent.Owner))
                continue;

            args.Progress = team.Members.Count > 1 ? 1f : 0f;
            return;
        }

        args.Progress = 0f;
    }

    #endregion

    #region Оповещения

    /// <summary>update_name(): имя команды собирается из фамилий братьев.</summary>
    private void UpdateTeamName(Entity<BloodBrotherTeamComponent> team)
    {
        var lastNames = new List<string>();
        foreach (var mindId in team.Comp.Members)
        {
            var name = GetBrotherName(mindId);
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            lastNames.Add(parts.Length > 0 ? parts[^1] : name);
        }

        team.Comp.TeamName = lastNames.Count switch
        {
            0 => Loc.GetString("blood-brother-team-name-empty"),
            1 => Loc.GetString("blood-brother-team-name-lone", ("name", lastNames[0])),
            _ => Loc.GetString("blood-brother-team-name", ("names", string.Join(" и ", lastNames))),
        };
    }

    private string GetBrotherName(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind))
            return Name(mindId);

        return mind.CharacterName ?? (mind.OwnedEntity is { } body ? Name(body) : Name(mindId));
    }

    /// <summary>greet(): приветствие с перечислением остальных братьев.</summary>
    private void Greet(Entity<BloodBrotherTeamComponent> team, EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null
            || !_players.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        var others = team.Comp.Members.Where(m => m != mindId).Select(GetBrotherName).ToList();
        var briefing = others.Count == 0
            ? Loc.GetString("blood-brother-role-greeting-solo")
            : Loc.GetString("blood-brother-role-greeting", ("brother", string.Join(", ", others)));

        var sound = CompOrNull<BloodBrotherRuleComponent>(team.Owner)?.GreetSoundNotification;
        _antag.SendBriefing(session, briefing, BriefingColor, sound);
    }

    /// <summary>Остальным братьям сообщают, кого пробудили и сколько пробуждений осталось.</summary>
    private void AnnounceNewBrother(Entity<BloodBrotherTeamComponent> team, EntityUid newMind)
    {
        var name = GetBrotherName(newMind);
        foreach (var mindId in team.Comp.Members)
        {
            if (mindId == newMind)
                continue;

            SendMessage(mindId, "blood-brother-awakened", ("name", name));
            if (team.Comp.BrothersLeft <= 0)
                SendMessage(mindId, "blood-brother-no-more");
        }
    }

    private void SendMessage(EntityUid mindId, LocId message, params (string, object)[] args)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null
            || !_players.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        var text = args.Length == 0 ? Loc.GetString(message) : Loc.GetString(message, args);
        _chat.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, session.Channel);
    }

    protected override void AppendRoundEndText(EntityUid uid,
        BloodBrotherRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (!TryComp<BloodBrotherTeamComponent>(uid, out var team) || team.Members.Count == 0)
            return;

        args.AddLine(Loc.GetString("blood-brother-round-end-team", ("name", team.TeamName)));
    }

    #endregion
}
