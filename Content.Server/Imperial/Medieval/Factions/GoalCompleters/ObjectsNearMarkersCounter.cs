using System.Linq;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class ObjectsNearMarkersCounter : FactionGoalCompleter
{
    [DataField(required: true)]
    public ProtoId<TagPrototype>[] RequiredTags = default!;

    [DataField(required: true)]
    public ProtoId<TagPrototype> MarkerTag = default!;

    [DataField(required: true)]
    public (int, int) RandomCount = (10, 15);

    [DataField(required: true)]
    public string RequiredName;

    public int Count = 1;

    public override FactionGoalCompleter CreateInstance()
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return new ObjectsNearMarkersCounter
        {
            RequiredTags = this.RequiredTags,
            MarkerTag = this.MarkerTag,
            RequiredName = this.RequiredName,
            Count = random.Next(RandomCount.Item1, RandomCount.Item2)
        };
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        var tagSystem = entMan.System<TagSystem>();
        var lookup = entMan.System<EntityLookupSystem>();
        var markers = entMan.AllEntities<TagComponent>().Where(e => tagSystem.HasTag(e.Owner, MarkerTag));

        var ents = new List<EntityUid>();
        foreach (var marker in markers)
        {
            var entities = lookup.GetEntitiesInRange(entMan.GetComponent<TransformComponent>(marker.Owner).Coordinates, 5f)
                .Where(e => RequiredTags.Any(tag => tagSystem.HasTag(e, tag)) && !ents.Contains(e));

            ents.AddRange(entities);
        }

        var result = ents.Count / (float)Count;
        result = Math.Clamp(result, 0, 1);
        return result;
    }

    public override string GetDesc(string desctiptionString)
    {
        return Loc.GetString(
            desctiptionString,
            ("count", Count),
            ("required", Loc.GetString(RequiredName)));
    }
}
