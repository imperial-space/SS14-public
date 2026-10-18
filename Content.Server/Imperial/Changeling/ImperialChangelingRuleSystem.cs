using System.Linq;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Shuttles.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Правило генокрадов: отбор, forge_objectives() и roundend_report() из changeling.dm.
/// </summary>
public sealed class ImperialChangelingRuleSystem : GameRuleSystem<ImperialChangelingRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly TargetObjectiveSystem _target = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly EmergencyShuttleSystem _shuttle = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ImperialChangelingSystem _changeling = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly Content.Shared.Roles.SharedRoleSystem _role = default!;

    private const string AbsorbObjective = "ImperialChangelingAbsorbObjective";
    private const string AbsorbMostObjective = "ImperialChangelingAbsorbMostObjective";
    private const string AbsorbLingObjective = "ImperialChangelingAbsorbLingObjective";
    private const string KillObjective = "ImperialChangelingKillObjective";
    private const string MaroonObjective = "ImperialChangelingMaroonObjective";
    private const string PairedMaroonObjective = "ImperialChangelingPairedMaroonObjective";
    private const string EscapeObjective = "EscapeShuttleObjective";
    private const string IdentityObjective = "ImperialChangelingEscapeIdentityObjective";
    private const string StealGroup = "TraitorObjectiveGroupSteal";
    private const string MindRole = "MindRoleImperialChangeling";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialChangelingRuleComponent, AfterAntagEntitySelectedEvent>(OnSelected);

        SubscribeLocalEvent<ImperialChangelingAbsorbConditionComponent, ObjectiveAssignedEvent>(OnAbsorbAssigned);
        SubscribeLocalEvent<ImperialChangelingAbsorbConditionComponent, ObjectiveAfterAssignEvent>(OnAbsorbAfterAssign, after: new[] { typeof(TargetObjectiveSystem) });
        SubscribeLocalEvent<ImperialChangelingAbsorbConditionComponent, ObjectiveGetProgressEvent>(OnAbsorbProgress);
        SubscribeLocalEvent<ImperialChangelingAbsorbMostConditionComponent, ObjectiveGetProgressEvent>(OnAbsorbMostProgress);
        SubscribeLocalEvent<ImperialChangelingAbsorbLingConditionComponent, ObjectiveGetProgressEvent>(OnAbsorbLingProgress);
        SubscribeLocalEvent<ImperialChangelingEscapeIdentityConditionComponent, ObjectiveAfterAssignEvent>(OnIdentityAfterAssign, after: new[] { typeof(TargetObjectiveSystem) });
        SubscribeLocalEvent<ImperialChangelingEscapeIdentityConditionComponent, ObjectiveGetProgressEvent>(OnIdentityProgress);
        SubscribeLocalEvent<ImperialChangelingPairedTargetComponent, ObjectiveAssignedEvent>(OnPairedAssigned);
    }

    private void OnSelected(Entity<ImperialChangelingRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeChangeling(args.EntityUid, ent.Comp);
    }

    /// <summary>Делает существо генокрадом с целями (и для админ-команды).</summary>
    public void MakeChangeling(EntityUid body, ImperialChangelingRuleComponent? rule = null, bool objectives = true)
    {
        // competitive_objectives: есть ли уже другой генокрад.
        var competitive = EntityQuery<ImperialChangelingComponent>().Any();
        _changeling.MakeChangeling(body);

        if (!_mind.TryGetMind(body, out var mindId, out var mind))
            return;

        if (!_role.MindHasRole<ImperialChangelingRoleComponent>(mindId))
            _role.MindAddRole(mindId, MindRole, mind, silent: true);

        rule?.Minds.Add(mindId);
        if (objectives)
            ForgeObjectives(mindId, mind, competitive);

        if (TryComp<ImperialChangelingComponent>(body, out var ling))
            _changeling.UpdateInfoUi((body, ling));
    }

    /// <summary>forge_objectives().</summary>
    private void ForgeObjectives(EntityUid mindId, MindComponent mind, bool competitive)
    {
        var escapePossible = true;

        switch (competitive ? _random.Next(1, 4) : 1)
        {
            case 1:
                _mind.TryAddObjective(mindId, mind, AbsorbObjective);
                break;
            case 2:
                _mind.TryAddObjective(mindId, mind, AbsorbMostObjective);
                break;
            case 3:
                _mind.TryAddObjective(mindId, mind, AbsorbLingObjective);
                break;
        }

        if (_random.Prob(0.6f) && _objectives.GetRandomObjective(mindId, mind, StealGroup, float.MaxValue) is { } steal)
            _mind.AddObjective(mindId, mind, steal);

        // destroy AI в SS14 нет: остаются убийство или «не дать сбежать».
        if (_random.Prob(0.7f))
        {
            _mind.TryAddObjective(mindId, mind, KillObjective);
        }
        else
        {
            // maroon + escape_with_identity с одной и той же жертвой.
            if (TryAddIdentityObjective(mindId, mind))
            {
                escapePossible = false;
                _mind.TryAddObjective(mindId, mind, PairedMaroonObjective);
            }
            else
            {
                _mind.TryAddObjective(mindId, mind, MaroonObjective);
            }
        }

        if (!escapePossible)
            return;

        if (_random.Prob(0.5f) || !TryAddIdentityObjective(mindId, mind))
            _mind.TryAddObjective(mindId, mind, EscapeObjective);
    }

    private bool TryAddIdentityObjective(EntityUid mindId, MindComponent mind)
    {
        var count = mind.Objectives.Count;
        _mind.TryAddObjective(mindId, mind, IdentityObjective);
        return mind.Objectives.Count > count;
    }

    #region Поглощение

    /// <summary>gen_amount_goal(): не больше, чем есть людей-игроков кроме генокрадов.</summary>
    private void OnAbsorbAssigned(Entity<ImperialChangelingAbsorbConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var target = _random.Next(ent.Comp.Min, ent.Comp.Max + 1);
        var players = 1;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { } body && HasComp<HumanoidProfileComponent>(body) && !HasComp<ImperialChangelingComponent>(body))
                players++;
        }

        ent.Comp.Target = Math.Min(target, players);
    }

    private void OnAbsorbAfterAssign(Entity<ImperialChangelingAbsorbConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent, Loc.GetString("imperial-changeling-objective-absorb", ("count", ent.Comp.Target)), args.Meta);
    }

    private void OnAbsorbProgress(Entity<ImperialChangelingAbsorbConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var absorbed = AbsorbedCount(args.MindId);
        args.Progress = ent.Comp.Target <= 0 ? 1f : Math.Clamp(absorbed / (float) ent.Comp.Target, 0f, 1f);
    }

    private void OnAbsorbMostProgress(Entity<ImperialChangelingAbsorbMostConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var ours = AbsorbedCount(args.MindId);
        var query = EntityQueryEnumerator<ImperialChangelingMindComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid != args.MindId && other.AbsorbedCount >= ours)
            {
                args.Progress = 0f;
                return;
            }
        }

        args.Progress = 1f;
    }

    private void OnAbsorbLingProgress(Entity<ImperialChangelingAbsorbLingConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = TryComp<ImperialChangelingMindComponent>(args.MindId, out var data) && data.EffectivePoints > data.InitialPoints ? 1f : 0f;
    }

    private int AbsorbedCount(EntityUid mindId)
    {
        if (TryComp<MindComponent>(mindId, out var mind) && TryComp<ImperialChangelingComponent>(mind.OwnedEntity, out var ling))
            return ling.AbsorbedCount;

        return TryComp<ImperialChangelingMindComponent>(mindId, out var data) ? data.AbsorbedCount : 0;
    }

    #endregion

    #region Побег под личиной

    /// <summary>update_explanation_text(): запомнить имя и есть ли у цели своя карта.</summary>
    private void OnIdentityAfterAssign(Entity<ImperialChangelingEscapeIdentityConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        if (!_target.GetTarget(ent, out var targetMind) || !TryComp<MindComponent>(targetMind, out var mind))
            return;

        ent.Comp.TargetName = mind.CharacterName;
        ent.Comp.TargetMissingId = !(mind.OwnedEntity is { } body && _idCard.TryFindIdCard(body, out var card) && card.Comp.FullName == mind.CharacterName);

        var job = _job.MindTryGetJobName(targetMind);
        var title = Loc.GetString(ent.Comp.TargetMissingId ? "imperial-changeling-objective-identity" : "imperial-changeling-objective-identity-id",
            ("targetName", ent.Comp.TargetName ?? "?"), ("job", job));
        _metaData.SetEntityName(ent, title, args.Meta);
    }

    /// <summary>check_completion(): сбежал, носит имя цели и (если была) её карту.</summary>
    private void OnIdentityProgress(Entity<ImperialChangelingEscapeIdentityConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0f;
        if (args.Mind.OwnedEntity is not { } body || !HasComp<HumanoidProfileComponent>(body) || ent.Comp.TargetName == null)
            return;

        if (!_mobState.IsAlive(body) || !_shuttle.IsTargetEscaping(body))
            return;

        if (Name(body) != ent.Comp.TargetName)
            return;

        if (ent.Comp.TargetMissingId || _idCard.TryFindIdCard(body, out var card) && card.Comp.FullName == ent.Comp.TargetName)
            args.Progress = 1f;
    }

    /// <summary>maroon_objective.target = identity_theft.target.</summary>
    private void OnPairedAssigned(Entity<ImperialChangelingPairedTargetComponent> ent, ref ObjectiveAssignedEvent args)
    {
        foreach (var objective in args.Mind.Objectives)
        {
            if (!HasComp<ImperialChangelingEscapeIdentityConditionComponent>(objective) || !_target.GetTarget(objective, out var target))
                continue;

            _target.SetTarget(ent, target.Value);
            return;
        }

        args.Cancelled = true;
    }

    #endregion

    protected override void AppendRoundEndText(EntityUid uid, ImperialChangelingRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.Minds.Count == 0)
            return;

        args.AddLine(Loc.GetString("imperial-changeling-roundend-header"));
        foreach (var mindId in component.Minds)
        {
            if (!TryComp<MindComponent>(mindId, out var mind))
                continue;

            var win = mind.OwnedEntity != null;
            foreach (var objective in mind.Objectives)
            {
                if (_objectives.GetProgress(objective, (mindId, mind)) is not >= 0.999f)
                    win = false;
            }

            args.AddLine(Loc.GetString("imperial-changeling-roundend-entry",
                ("name", mind.CharacterName ?? "?"),
                ("user", mind.UserId is { } userId && _players.TryGetSessionById(userId, out var session) ? session.Name : "?"),
                ("count", AbsorbedCount(mindId))));
            args.AddLine(Loc.GetString(win ? "imperial-changeling-roundend-win" : "imperial-changeling-roundend-lose"));
        }
    }
}
