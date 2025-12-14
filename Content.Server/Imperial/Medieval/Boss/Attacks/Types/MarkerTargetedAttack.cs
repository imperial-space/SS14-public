using System.Linq;
using Content.Shared.Tag;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class MarkerTargetedAttack : BossAttack
{
    [DataField]
    public (int, int) CountLimits = (1, 3);

    [DataField(required: true)]
    public string MarkerTag;

    public override IEnumerable<EntityUid> PickTargets(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var tagSystem = entMan.System<TagSystem>();

        var bossXform = entMan.GetComponent<TransformComponent>(boss);
        if (!bossXform.GridUid.HasValue)
            return Enumerable.Empty<EntityUid>();

        var gridXform = entMan.GetComponent<TransformComponent>(bossXform.GridUid.Value);
        var markers = new List<EntityUid>();

        var children = gridXform.ChildEnumerator;

        while (children.MoveNext(out var child))
        {
            if (!tagSystem.HasTag(child, MarkerTag))
                continue;

            markers.Add(child);
        }

        Random.Shared.Shuffle(markers);

        return markers.Take(Math.Min(random.Next(CountLimits.Item1, CountLimits.Item2), markers.Count));
    }
}
