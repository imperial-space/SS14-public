using System.Linq;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Imperial.Heretic;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Server.Revolutionary.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Mind;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

public sealed class HereticRuleSystem : GameRuleSystem<HereticRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem      _antag   = default!;
    [Dependency] private readonly HereticSystem             _heretic = default!;
    [Dependency] private readonly MindSystem                _mind    = default!;
    [Dependency] private readonly RoundEndSystem            _roundEnd = default!;
    [Dependency] private readonly HereticRealityRiftSystem  _rift    = default!;
    [Dependency] private readonly IPrototypeManager         _proto   = default!;
    [Dependency] private readonly IRobustRandom             _random  = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticRuleComponent, AfterAntagEntitySelectedEvent>(OnHereticSelected);
        SubscribeLocalEvent<HereticAscendedEvent>(OnHereticAscended);
    }

    private void OnHereticSelected(Entity<HereticRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _heretic.MakeHeretic(args.EntityUid);

        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out var mind))
        {
            Log.Error($"[Heretic] No mind found for {ToPrettyString(args.EntityUid)}");
            return;
        }

        ent.Comp.HereticMinds.Add(mindId);
        _heretic.AssignNamedTargets(args.EntityUid, mindId);

        const int requiredSacrifices = 4;
        var requiredKnowledge = 0;

        _mind.TryAddObjective(mindId, mind, "HereticSacrificeObjective");
        if (TryComp<HereticSacrificeConditionComponent>(mind.Objectives[^1], out var sacrificeCondition))
            sacrificeCondition.RequiredSacrifices = requiredSacrifices;

        _mind.TryAddObjective(mindId, mind, "HereticMajorSacrificeObjective");

        _mind.TryAddObjective(mindId, mind, "HereticResearchObjective");
        if (TryComp<HereticResearchConditionComponent>(mind.Objectives[^1], out var researchCondition))
        {
            requiredKnowledge = GetDynamicResearchTarget();
            researchCondition.RequiredKnowledge = requiredKnowledge;
        }

        _heretic.SetAscensionTargets(args.EntityUid, requiredSacrifices, requiredKnowledge);

        _mind.TryAddObjective(mindId, mind, "HereticAscensionObjective");

        SpawnInitialRifts(args.EntityUid);
    }

    private int GetDynamicResearchTarget()
    {
        // Average size of a single path (the heretic only ever commits to one), not the sum of all paths.
        var pathCounts = new Dictionary<HereticPath, int>();
        var startingCount = 0;
        var freeCount = 0;

        foreach (var knowledge in _proto.EnumeratePrototypes<HereticKnowledgePrototype>())
        {
            if (knowledge.Path == HereticPath.General)
            {
                freeCount++;
                if (knowledge.Prerequisites.Count == 0 && knowledge.PrerequisitesAny.Count == 0)
                    startingCount++;
                continue;
            }

            pathCounts.TryGetValue(knowledge.Path, out var count);
            pathCounts[knowledge.Path] = count + 1;
        }

        var mainPathCount = pathCounts.Count == 0 ? 0 : (int) Math.Round(pathCounts.Values.Average());

        return 1 + mainPathCount + startingCount + (int) Math.Ceiling(freeCount / 3f) + _random.Next(2, 5);
    }

    private void SpawnInitialRifts(EntityUid heretic)
    {
        const int count = 5;
        var spawned = 0;
        var attempts = 0;

        while (spawned < count && attempts < count * 4)
        {
            attempts++;
            if (!TryFindRandomTile(out _, out _, out _, out var coords))
                break;

            _rift.SpawnRift(coords);
            spawned++;
        }

        if (spawned < count)
            Log.Warning($"[Heretic] SpawnInitialRifts: spawned only {spawned}/{count} rifts after {attempts} attempts");
    }

    protected override void AppendRoundEndText(EntityUid uid, HereticRuleComponent comp, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, comp, gameRule, ref args);

        var antags = _antag.GetAntagIdentifiers(uid).ToList();
        if (antags.Count == 0)
            return;

        args.AddLine(Loc.GetString("heretic-roundend-header"));
        args.AddLine("");

        foreach (var (entityUid, sessionData, name) in antags)
        {
            if (!_heretic.TryGetRoundEndData(entityUid, out var data))
            {
                args.AddLine(Loc.GetString("heretic-roundend-entry", ("name", name), ("user", sessionData.UserName)));
                args.AddLine("");
                continue;
            }

            args.AddLine($"[bold]{name}[/bold] ({sessionData.UserName})");

            var pathColor = GetPathColor(data.CurrentPath);
            var pathName = Loc.GetString($"heretic-path-{data.CurrentPath.ToString().ToLowerInvariant()}");
            args.AddLine($"  {Loc.GetString("heretic-roundend-path")}: [color={pathColor}]◆[/color] [bold]{pathName}[/bold]");
            args.AddLine("");

            args.AddLine($"  [bold]{Loc.GetString("heretic-roundend-objectives")}:[/bold]");

            var sacrificeDone = data.SacrificeCount >= data.RequiredSacrifices;
            var sacrificeMark = sacrificeDone ? "[color=#55FF55]✔[/color]" : "[color=#FF5555]✘[/color]";
            args.AddLine($"    {sacrificeMark} {Loc.GetString("heretic-roundend-obj-sacrifice", ("req", data.RequiredSacrifices))} ({data.SacrificeCount}/{data.RequiredSacrifices})");

            var majorMark = data.HighValueSacrificeCount >= 1 ? "[color=#55FF55]✔[/color]" : "[color=#FF5555]✘[/color]";
            args.AddLine($"    {majorMark} {Loc.GetString("heretic-roundend-obj-major")}");

            var researchDone = data.TotalKnowledgeGained >= data.RequiredKnowledge;
            var researchMark = researchDone ? "[color=#55FF55]✔[/color]" : "[color=#FF5555]✘[/color]";
            args.AddLine($"    {researchMark} {Loc.GetString("heretic-roundend-obj-research", ("req", data.RequiredKnowledge))} ({data.TotalKnowledgeGained}/{data.RequiredKnowledge})");

            var ascendMark = data.AscensionTriggered ? "[color=#55FF55]✔[/color]" : "[color=#FF5555]✘[/color]";
            args.AddLine($"    {ascendMark} {Loc.GetString("heretic-roundend-obj-ascension")}");
            args.AddLine("");

            if (data.ResearchedKnowledge.Count > 0)
            {
                args.AddLine($"  [bold]{Loc.GetString("heretic-roundend-knowledge", ("count", data.ResearchedKnowledge.Count))}:[/bold]");
                foreach (var knowledgeId in data.ResearchedKnowledge)
                {
                    if (!_proto.TryIndex(knowledgeId, out var knowledge))
                        continue;
                    var kColor = GetPathColor(knowledge.Path);
                    var kName = Loc.GetString(knowledge.Name);
                    args.AddLine($"    [color={kColor}]◆[/color] {kName}");
                }
            }

            args.AddLine("");
        }
    }

    private static string GetPathColor(HereticPath path) => path switch
    {
        HereticPath.Ash    => "#FF6633",
        HereticPath.Moon   => "#88AAFF",
        HereticPath.Lock   => "#FFCC44",
        HereticPath.Flesh  => "#CC3333",
        HereticPath.Void   => "#4466CC",
        HereticPath.Blade  => "#BBBBBB",
        HereticPath.Rust   => "#BB6600",
        HereticPath.Cosmos => "#AA44CC",
        _                  => "#AAAAAA"
    };

    private void OnHereticAscended(HereticAscendedEvent ev)
    {
        _roundEnd.RequestRoundEnd(TimeSpan.FromMinutes(10), checkCooldown: false);
    }
}

/// <summary>Raised on the server when a heretic completes their ascension ritual.</summary>
public sealed class HereticAscendedEvent : EntityEventArgs
{
    public EntityUid Heretic;
    public HereticAscendedEvent(EntityUid heretic) => Heretic = heretic;
}
