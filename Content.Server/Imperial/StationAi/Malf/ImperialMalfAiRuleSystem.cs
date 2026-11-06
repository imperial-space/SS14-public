using System.Linq;
using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Console;
using Robust.Shared.Random;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>
/// Правило сбойного ИИ (malf_ai.dm): выбор ИИ станции, forge_ai_objectives() — особая цель, убийство и выживание.
/// </summary>
public sealed class ImperialMalfAiRuleSystem : GameRuleSystem<ImperialMalfAiRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ImperialMalfAiSystem _malf = default!;
    [Dependency] private readonly EmergencyShuttleSystem _shuttle = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private const string BlockObjective = "ImperialMalfBlockObjective";
    private const string RobotArmyObjective = "ImperialMalfRobotArmyObjective";
    private const string KillObjective = "KillRandomPersonObjective";
    private const string SurviveObjective = "ImperialMalfSurviveObjective";

    private const float ForgeObjectivesChance = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialMalfAiRuleComponent, AfterAntagEntitySelectedEvent>(OnSelected);
        SubscribeLocalEvent<ImperialMalfBlockConditionComponent, ObjectiveGetProgressEvent>(OnBlockProgress);
        SubscribeLocalEvent<ImperialMalfRobotArmyConditionComponent, ObjectiveGetProgressEvent>(OnRobotArmyProgress);
    }

    private void OnSelected(Entity<ImperialMalfAiRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeMalf(args.EntityUid, ent.Comp);
    }

    /// <summary>Сделать ИИ сбойным с целями (и для админ-команды).</summary>
    public bool MakeMalf(EntityUid ai, ImperialMalfAiRuleComponent? rule = null)
    {
        if (!_malf.MakeMalf(ai))
            return false;

        rule?.Ais.Add(ai);
        if (_mind.TryGetMind(ai, out var mindId, out var mind))
            ForgeObjectives(mindId, mind);
        return true;
    }

    /// <summary>forge_ai_objectives(): особая цель (блокада или армия роботов), убийство, выживание.</summary>
    private void ForgeObjectives(EntityUid mindId, MindComponent mind)
    {
        _mind.TryAddObjective(mindId, mind, _random.Prob(ForgeObjectivesChance) ? BlockObjective : RobotArmyObjective);
        _mind.TryAddObjective(mindId, mind, KillObjective);
        _mind.TryAddObjective(mindId, mind, SurviveObjective);
    }

    /// <summary>check_completion(): шаттл не улетел — цель выполнена; улетел с живыми органиками — провал.</summary>
    private void OnBlockProgress(Entity<ImperialMalfBlockConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (!_shuttle.ShuttlesLeft)
        {
            args.Progress = 1f;
            return;
        }

        var query = EntityQueryEnumerator<HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_mobs.IsAlive(uid) || !_mind.TryGetMind(uid, out _, out _) || !_shuttle.IsTargetEscaping(uid))
                continue;

            args.Progress = 0f;
            return;
        }

        args.Progress = 1f;
    }

    private void OnRobotArmyProgress(Entity<ImperialMalfRobotArmyConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (args.Mind.OwnedEntity is not { } ai)
        {
            args.Progress = 0f;
            return;
        }

        var count = 0;
        var query = EntityQueryEnumerator<ImperialAiBorgLinkComponent>();
        while (query.MoveNext(out var borg, out var link))
        {
            if (link.Ai == ai && !_mobs.IsDead(borg))
                count++;
        }

        args.Progress = Math.Clamp((float)count / ent.Comp.Required, 0f, 1f);
    }
}
