using System.Linq;
using Content.Server.MagicBarrier;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class RelationsCounter : FactionGoalCompleter
{
    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> Faction = default!;

    [DataField(required: true)]
    public ProtoId<FactionRelationsPrototype> Relation = default!;

    [DataField]
    public (int, int) RandomCount = (2, 3);

    public int Count = 0;

    public override FactionGoalCompleter CreateInstance()
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return new RelationsCounter
        {
            Faction = this.Faction,
            Relation = this.Relation,
            Count = random.Next(RandomCount.Item1, RandomCount.Item2)
        };
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        var factionSystem = entMan.System<MedievalFactionsSystem>();
        if (!factionSystem.TryGetFactionDataContainer(out var container))
            return 0f;

        if (!container.Value.Comp.Relations.TryGetValue(Faction, out var relations))
            return 0f;

        var count = relations.Where(x => x.Value == Relation).Count();

        var result = count / (float)Count;
        result = Math.Clamp(result, 0, 1);

        return result;
    }

    public override string GetDesc(string desctiptionString)
    {
        return Loc.GetString(
            desctiptionString,
            ("count", Count),
            ("relation", Loc.GetString(Relation)));
    }
}
