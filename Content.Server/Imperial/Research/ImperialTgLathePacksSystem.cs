using System.Linq;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Research;

/// <summary>
/// Рецепты дизайнов TG для станков без правки ванильных прототипов станков:
/// пакет ImperialTgStatic&lt;ID станка&gt; доступен всегда, ImperialTgDynamic&lt;ID станка&gt; — после изучения.
/// Учитываются и родители прототипа (гиперконвекционный протолат получает пакеты протолата).
/// </summary>
public sealed class ImperialTgLathePacksSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private const string StaticPrefix = "ImperialTgStatic";
    private const string DynamicPrefix = "ImperialTgDynamic";

    private readonly Dictionary<string, (List<LatheRecipePackPrototype> Static, List<LatheRecipePackPrototype> Dynamic)> _cache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, LatheGetRecipesEvent>(OnGetRecipes);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _cache.Clear());
    }

    private void OnGetRecipes(Entity<LatheComponent> ent, ref LatheGetRecipesEvent args)
    {
        if (args.Lathe != ent.Owner || MetaData(ent).EntityPrototype is not { } proto)
            return;

        var (statics, dynamics) = GetPacks(proto.ID);
        foreach (var pack in statics)
        {
            foreach (var recipe in pack.Recipes)
            {
                args.Recipes.Add(recipe);
            }
        }

        if (dynamics.Count == 0)
            return;

        TryComp<TechnologyDatabaseComponent>(ent, out var database);
        foreach (var pack in dynamics)
        {
            foreach (var recipe in pack.Recipes)
            {
                if (args.GetUnavailable || database != null && Enumerable.Contains(database.UnlockedRecipes, recipe))
                    args.Recipes.Add(recipe);
            }
        }
    }

    private (List<LatheRecipePackPrototype>, List<LatheRecipePackPrototype>) GetPacks(string protoId)
    {
        if (_cache.TryGetValue(protoId, out var cached))
            return cached;

        var result = (new List<LatheRecipePackPrototype>(), new List<LatheRecipePackPrototype>());
        Collect(protoId, result, new HashSet<string>());
        _cache[protoId] = result;
        return result;
    }

    private void Collect(string protoId, (List<LatheRecipePackPrototype> Static, List<LatheRecipePackPrototype> Dynamic) result, HashSet<string> visited)
    {
        if (!visited.Add(protoId))
            return;

        if (_proto.TryIndex<LatheRecipePackPrototype>(StaticPrefix + protoId, out var s))
            result.Static.Add(s);

        if (_proto.TryIndex<LatheRecipePackPrototype>(DynamicPrefix + protoId, out var d))
            result.Dynamic.Add(d);

        if (!_proto.TryIndex<EntityPrototype>(protoId, out var proto) || proto.Parents == null)
            return;

        foreach (var parent in proto.Parents)
        {
            Collect(parent, result, visited);
        }
    }
}
