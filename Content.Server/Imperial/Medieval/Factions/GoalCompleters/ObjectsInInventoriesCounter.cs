using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class ObjectsInInventoriesCounter : FactionGoalCompleter
{
    [DataField(required: true)]
    public ProtoId<TagPrototype>[] RequiredTags = default!;

    [DataField(required: true)]
    public ProtoId<MedievalFactionPrototype> Faction = default!;

    [DataField(required: true)]
    public (int, int) RandomCount = (10, 15);

    [DataField(required: true)]
    public string RequiredName;

    public int Count = 1;

    public override FactionGoalCompleter CreateInstance()
    {
        var random = IoCManager.Resolve<IRobustRandom>();

        return new ObjectsInInventoriesCounter
        {
            RequiredTags = this.RequiredTags,
            Faction = this.Faction,
            RequiredName = this.RequiredName,
            Count = random.Next(RandomCount.Item1, RandomCount.Item2)
        };
    }

    public override float GetCompletion(IEntityManager entMan)
    {
        var factionSys = entMan.System<MedievalFactionsSystem>();
        if (!factionSys.TryGetFactionDataContainer(out var factionData))
            return 0f;

        int count = 0;

        foreach (var member in factionData.Value.Comp.CachedMembers.GetOrNew(Faction))
        {
            if (!factionSys.GetFactionMemberById(member.Key, out var uid))
                continue;

            count += GetCount(uid.Value, entMan);
        }

        var result = count / (float)Count;
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

    private int GetCount(EntityUid uid, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<ContainerManagerComponent>(uid, out var currentManager))
            return 0;

        var containerStack = new Stack<ContainerManagerComponent>();
        var count = 0;

        // recursively check each container for the item
        // checks inventory, bag, implants, etc.
        do
        {
            foreach (var container in currentManager.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    // check if this is the item
                    count += CheckStealTarget(entity, entMan);

                    // if it is a container check its contents
                    if (entMan.TryGetComponent<ContainerManagerComponent>(entity, out var containerManager))
                        containerStack.Push(containerManager);
                }
            }
        } while (containerStack.TryPop(out currentManager));

        return count;
    }

    private int CheckStealTarget(EntityUid entity, IEntityManager entMan)
    {
        var tagSys = entMan.System<TagSystem>();
        if (!tagSys.HasAnyTag(entity, RequiredTags))
            return 0;

        return entMan.TryGetComponent<StackComponent>(entity, out var stack) ? stack.Count : 1;
    }
}
