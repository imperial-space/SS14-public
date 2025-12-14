using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking;
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Server.Imperial.Medieval.Factions.Components;
using Content.Server.Imperial.Medieval.Factions;
using Robust.Shared.Utility;
using Content.Shared.Imperial.Medieval.Factions;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class FactionGoalsRuleSystem : GameRuleSystem<FactionGoalsRuleComponent>
{
    [Dependency] private readonly MedievalFactionsSystem _factions = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextGoalUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionGoalsRuleComponent, MapInitEvent>(OnGoalsMapInit);
        SubscribeLocalEvent<MedievalFactionMemberComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<KillGoalTargetComponent, DamageChangedEvent>(OnTargetDamage);
        SubscribeLocalEvent<KillGoalTargetComponent, MobStateChangedEvent>(OnTargetMobStateChanged);
    }

    public void OnGoalsMapInit(EntityUid uid, FactionGoalsRuleComponent comp, MapInitEvent args)
    {
        if (!_factions.EnsureFactionDataContainer(out var data))
            return;

        foreach (var item in comp.Goals)
        {
            var pack = _proto.Index(_random.Pick(item.Value));
            var goals = pack.Goals;
            data.Value.Comp.Goals.TryAdd(item.Key, new List<FactionGoalData>());
            data.Value.Comp.Goals[item.Key].AddRange(goals.Select(goal => new FactionGoalData(_proto.Index(goal), EntityManager)));
        }
    }

    private void OnMobStateChanged(EntityUid uid, MedievalFactionMemberComponent comp, ref MobStateChangedEvent args)
    {
        if (!_factions.TryGetFactionDataContainer(out var data))
            return;
        if (args.NewMobState != MobState.Dead)
            return;

        data.Value.Comp.Deaths.GetOrNew(comp.Faction);
        data.Value.Comp.Deaths[comp.Faction]++;
    }

    private void OnTargetDamage(EntityUid uid, KillGoalTargetComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (TryComp<MedievalFactionMemberComponent>(args.Origin, out var member))
            comp.LastHitFaction = member.Faction;
    }

    private void OnTargetMobStateChanged(EntityUid uid, KillGoalTargetComponent comp, ref MobStateChangedEvent args)
    {
        if (!_factions.TryGetFactionDataContainer(out var data))
            return;

        if (args.NewMobState != MobState.Dead)
            return;

        if (comp.LastHitFaction == null)
            return;

        var kills = data.Value.Comp.MobKills.GetOrNew(comp.LastHitFaction.Value);
        if (!kills.ContainsKey(comp.TargetId))
            kills[comp.TargetId] = 0;

        kills[comp.TargetId]++;
    }

    protected override void AppendRoundEndText(EntityUid uid,
        FactionGoalsRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (!_factions.TryGetFactionDataContainer(out var data))
            return;

        var container = data.Value.Comp;

        foreach (var item in container.Goals)
        {
            var proto = _proto.Index(item.Key);
            args.AddLine(Loc.GetString("faction-goal-round-end-header", ("faction", proto.Name)));

            foreach (var goal in item.Value)
            {
                var completion = goal.Completer.GetCompletion(EntityManager);
                var goalProto = _proto.Index(goal.GoalProto);

                args.AddLine(Loc.GetString("faction-goal-round-end-text",
                    ("goal", Loc.GetString(goalProto.Name)),
                    ("color", completion >= 1f ? "green" : "crimson"),
                    ("completion", $"{completion * 100f:0.00}%")));
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_nextGoalUpdate > _timing.CurTime)
            return;

        _nextGoalUpdate = _timing.CurTime + TimeSpan.FromSeconds(30);

        if (!_factions.EnsureFactionDataContainer(out var data))
            return;

        _factions.UpdateGoals(data.Value);
    }
}
