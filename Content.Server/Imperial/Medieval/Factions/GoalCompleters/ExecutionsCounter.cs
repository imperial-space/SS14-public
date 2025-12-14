using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class ExecutionsCounter : FactionGoalCompleter
{
    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> Faction = default!;

    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> TargetFaction = default!;

    [DataField(required: true)]
    public (int, int) RandomCount = (5, 7);

    public int Count = 1;

    public override FactionGoalCompleter CreateInstance()
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return new ExecutionsCounter
        {
            Faction = this.Faction,
            TargetFaction = this.TargetFaction,
            Count = random.Next(RandomCount.Item1, RandomCount.Item2)
        };
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        var factionSystem = entMan.System<MedievalFactionsSystem>();
        if (!factionSystem.TryGetFactionDataContainer(out var container))
            return 0f;

        var deaths = container.Value.Comp.Executions.GetOrNew(Faction);
        if (!deaths.TryGetValue(TargetFaction, out var count))
            deaths[TargetFaction] = 0;

        var result = deaths[TargetFaction] / (float)Count;
        result = Math.Clamp(result, 0, 1);
        return result;
    }

    public override string GetDesc(string desctiptionString)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>().Index(TargetFaction);
        return Loc.GetString(
            desctiptionString,
            ("targetFaction", Loc.GetString(proto.Name)),
            ("count", Count));
    }
}
