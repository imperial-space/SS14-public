using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MobKillCounter : FactionGoalCompleter
{
    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> Faction = default!;

    [DataField(required: true)]
    public (int, int) RandomCount = (10, 15);

    [DataField(required: true)]
    public string Id;

    [DataField(required: true)]
    public string TargetName;

    public int Count = 1;

    public override FactionGoalCompleter CreateInstance()
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return new MobKillCounter
        {
            Faction = this.Faction,
            Id = this.Id,
            Count = random.Next(RandomCount.Item1, RandomCount.Item2)
        };
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        var factionSystem = entMan.System<MedievalFactionsSystem>();
        if (!factionSystem.TryGetFactionDataContainer(out var container))
            return 0f;

        var kills = container.Value.Comp.MobKills.GetValueOrDefault(Faction, new());
        var targetKills = kills.GetValueOrDefault(Id, 0);

        var result = targetKills / (float)Count;
        result = Math.Clamp(result, 0, 1);
        return result;
    }

    public override string GetDesc(string desctiptionString)
    {
        return Loc.GetString(
            desctiptionString,
            ("count", Count),
            ("target", Loc.GetString(TargetName)));
    }
}
