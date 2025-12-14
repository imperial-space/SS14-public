using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class FactionMembersDeathsCounter : FactionGoalCompleter
{
    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> Faction = default!;

    [DataField(required: true)]
    public (int, int) RandomCount = (10, 15);

    [DataField]
    public bool LessThan = true;

    public int Count = 1;

    public override FactionGoalCompleter CreateInstance()
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return new FactionMembersDeathsCounter
        {
            Faction = this.Faction,
            LessThan = this.LessThan,
            Count = random.Next(RandomCount.Item1, RandomCount.Item2)
        };
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        var factionSystem = entMan.System<MedievalFactionsSystem>();
        if (!factionSystem.TryGetFactionDataContainer(out var container))
            return 0f;

        var deaths = container.Value.Comp.Deaths.GetValueOrDefault(Faction, 0);

        if (LessThan)
            return deaths < Count ? 1f : 0f;

        var result = deaths / (float)Count;
        result = Math.Clamp(result, 0, 1);
        return result;
    }

    public override string GetDesc(string desctiptionString)
    {
        return Loc.GetString(
            desctiptionString,
            ("count", Count));
    }
}
