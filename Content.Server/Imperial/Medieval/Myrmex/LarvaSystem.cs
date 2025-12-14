using System.Linq;
using Content.Server.Polymorph.Systems;
using Content.Shared.Imperial.Medieval.Myrmex;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Myrmex;

public sealed partial class LarvaSystem : EntitySystem
{

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LarvaComponent, LarvaFeedEvent>(OnLarvaFeed);
    }

    private bool Matches(LarvaGrowPrototype proto, List<EntityPrototype> input)
    {
        var recipe = proto.Recipe;

        if (recipe.Count != input.Count)
            return false;

        var inputIDs = input.Select(e => e.ID).ToList();

        for (int i = 0; i < recipe.Count; i++)
        {
            var food = recipe[i];
            if (food != "Any" && food != input[i])
                return false;
        }


        if (proto.Condition == null)
            return true;

        return proto.Condition.Check(inputIDs);
    }

    private void OnLarvaFeed(EntityUid uid, LarvaComponent component, ref LarvaFeedEvent args)
    {
        component.Eaten.Add(args.Eaten);
        var recipePrototypes = _prototype.EnumeratePrototypes<LarvaGrowPrototype>().ToList();
        recipePrototypes = recipePrototypes.OrderBy(e => e.Priority).ToList();


        foreach (var recipe in recipePrototypes)
        {
            var matches = Matches(recipe, component.Eaten);

            if (!matches)
                continue;

            _polymorph.PolymorphEntity(uid, recipe.ResultEntity.Id);
        }
    }
}
