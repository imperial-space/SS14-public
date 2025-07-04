using System.Diagnostics.CodeAnalysis;
using Content.Server.GameTicking;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Random.Helpers;
using Robust.Server.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using System.Linq;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Utility;
using Robust.Server.Upload;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Examine;
using Content.Server.MedievalPotionChecker.Components;
using Robust.Shared.Containers;
using Content.Shared.Chemistry.Components;
using Content.Shared.Fluids;
using Content.Shared.Imperial.Medieval.Chemistry;
using Robust.Shared.GameObjects;

namespace Content.Server.ChemistryRandomization;

public sealed class ChemistryRandomizationSystem : EntitySystem // TODO: Maybe rewrite it to not use vanilla chemistry
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly GamePrototypeLoadManager _uploadManager = default!;
    [Dependency] private readonly IEntitySystemManager _ent = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    public readonly List<string> BasicReagentList = new()
    {
        "MedievalFlowerLipad",
        "MedievalFlowerVetr",
        "MedievalFlowerCryo",
        "MedievalLootCrabDevil",
        "MedievalLootCrab",
        "MedievalLootHunter",
        "MedievalLootEgg",
        "MedievalLootHog",
        "MedievalLootGoblin",
        "MedievalLootAnt",
        "MedievalLootDevilMouse",
        "Blood",
        "DemonsBlood",
        "MedievalFlowerLava",
        "MedievalMushroom1",
        "MedievalMushroom2",
        "MedievalMushroom3"
    };
    public readonly List<string> EasyPotionList = new()
    {
        "medievalexplosion",
        "medievalhealburn",
        "medievalhealbloodandoxygen",
        "medievalacid",
        "medievalradiation",
        "medievaldrughealmech",
        "medievalvomit",
        "Ammonia",
        "PlantBGone",
        "Left4Zed",
        "Diethylamine",
        "medievalslow",
        "medievalfast",
        "medievalstamina",
        "medievalsmoke",
        "Antihol",
        "medievalcryoexplosion",
        "medievalhealslash",
        "medievalhealpiercing",
        "medievalhealblunt",
        "medievalmute",
        "medievalblind",
        "medievaljitter",
        "medievalsleep",
        "medievalignite",
        "medievalhealandstopblood"
    };
    public readonly List<string> MediumPotionList = new()
    {
        "medievaladvancedhealeverything",
        "medievaladvancedhealslash",
        "medievaladvancedhealpiercing",
        "medievaladvancedhealblunt",
        "medievaladvancedhealburn",
        "medievaladvancedacidradiation",
        "medievaladvancedfast",
        "medievaladvancedslow",
        "medievaladvancedpacifism",
        "medievaladvancedteleport",
        "medievaladvancedhealandstopblood",
        "medievaladvancedstimulant",
        "medievaladvancedignite",
        "medievaladvancednocturnblood",
        "medievaladvancedsmokeandexplosion"
    };
    public readonly List<string> HardPotionList = new()
    {
        "medievalbestexplosion",
        "medievalbestheal",
        "medievalbestpoison",
        "medievalbeststimulant"
    };
    public readonly List<string> Descriptions = new()
    {
        "reagent-physical-desc-opaque",
        "reagent-physical-desc-cloudy",
        "reagent-physical-desc-glowing",
        "reagent-physical-desc-syrupy",
        "reagent-physical-desc-cold",
        "reagent-physical-desc-acidic",
        "reagent-physical-desc-bubbling",
        "reagent-physical-desc-strong-smelling"
    };
    public readonly List<string> Flavors = new()
    {
        "alcohol",
        "ambrosia",
        "medicine",
        "cold",
        "funny",
        "sharp",
        "mushroom",
        "Pyrotechnic",
        "sweet",
        "metallic"
    };
    public readonly Dictionary<string, Dictionary<string, List<string>>> CurrentRecipes = new()
    {
        ["easy"] = new(),
        ["medium"] = new(),
        ["hard"] = new()
    };
    public readonly Dictionary<string, MappingDataNode> OriginalReagentPrototypes = new();
    public readonly Dictionary<string, MappingDataNode> OriginalReactionPrototypes = new();
    public delegate bool Can<T>(T value);
    public delegate bool Can<T, T2>(T key, T2 value);
    public override void Initialize()
    {
        if (!_prototype.TryGetKindType("reagent", out var reagentproto))
            return;
        if (!_prototype.TryGetKindType("reaction", out var reactionproto))
            return;

        var list = new List<string>();
        list.AddRange(EasyPotionList);
        list.AddRange(MediumPotionList);
        list.AddRange(HardPotionList);
        foreach (var id in list)
        {
            if (_prototype.TryGetMapping(reagentproto, id, out var reagent))
                OriginalReagentPrototypes.Add(id, reagent);

            if (_prototype.TryGetMapping(reactionproto, id, out var reaction))
                OriginalReactionPrototypes.Add(id, reaction);
        }
        SubscribeLocalEvent<RoundStartAttemptEvent>(TryStart);
        SubscribeLocalEvent<SolutionContainerManagerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<MedievalRandomChemistryRecipeComponent, MapInitEvent>(Init);
    }
    public void Init(EntityUid uid, MedievalRandomChemistryRecipeComponent component, MapInitEvent args)
    {
        if (component.Weights.Count() == 0)
            return;
        var type = _random.Pick(component.Weights);
        var randomrecipe = _random.Pick(CurrentRecipes[type]);
        var str = Loc.GetString("imperial-medieval-recipewritten");
        foreach (var reagent in randomrecipe.Value)
        {
            str = $"{str}{Environment.NewLine}- {Loc.GetString(_prototype.Index<ReagentPrototype>(reagent).LocalizedName)}";
        }
        _meta.SetEntityDescription(uid, str);
    }
    public void OnExamine(EntityUid uid, SolutionContainerManagerComponent component, ExaminedEvent args)
    {
        if (!HasComp<MedievalPotionCheckerComponent>(args.Examiner))
            return;
        if (!TryComp<ContainerManagerComponent>(uid, out var contman))
            return;

        if (component.Containers == null)
            return;

        var str = $"{Loc.GetString("imperial-medieval-chemistry-examine")}{Environment.NewLine}";
        var addedsomething = false;
        var addedeffects = new List<string>();
        foreach (var key in component.Containers)
        {
            if (!contman.TryGetContainer($"solution@{key}", out var container))
                continue;
            var solution = EnsureComp<SolutionComponent>(((ContainerSlot)container).ContainedEntity!.Value).Solution;
            foreach (var reagent in solution.Contents)
            {
                var proto = _prototype.Index<ReagentPrototype>(reagent.Reagent.Prototype);
                if (proto.ShowInBook)
                    continue;

                if (proto.Metabolisms == null)
                    continue;

                foreach (var (_, effectentry) in proto.Metabolisms)
                {
                    var entry = effectentry.MakeGuideEntry(_prototype, _ent);
                    var effects = string.Empty;
                    foreach (var effect in entry.EffectDescriptions)
                    {
                        if (addedeffects.Contains(effect))
                            continue;
                        effects = $"{effects}{Environment.NewLine}- {effect}";
                        addedeffects.Add(effect);
                    }
                    str = $"{str}{effects}{Environment.NewLine}";
                    addedsomething = true;
                }
            }
        }
        if (!addedsomething)
            return;
        args.PushMarkup(str);
    }
    public bool BasicCan<T>(T value)
    {
        return true;
    }
    public bool BasicCan<T, T2>(T key, T2 value)
    {
        return true;
    }
    public bool TryRandomize<T>(IList<T> available, Can<T> method, int amount, out List<T> result)
    {
        result = new();
        var x = 0;
        while (x < amount)
        {
            if (available.Count == 0)
                return false;
            var got = _random.PickAndTake(available);
            if (!method(got))
                continue;

            result.Add(got);
            x += 1;
        }
        return true;
    }
    public bool TryRandomize<T, T2>(IList<T> availablekeys, IList<T2> availablevalues, Can<T, T2> method, int amount, out Dictionary<T, T2> result, out List<T> keysused)
        where T : notnull
        where T2 : notnull
    {
        result = new();
        keysused = new();
        var x = 0;
        while (x < amount)
        {
            if (availablekeys.Count == 0 || availablevalues.Count == 0)
                return false;
            var key = _random.PickAndTake(availablekeys);
            var value = _random.PickAndTake(availablevalues);
            if (!method(key, value))
                continue;

            result.Add(key, value);
            keysused.Add(key);
            x += 1;
        }
        return true;
    }
    private string GetString(Dictionary<string, List<string>> list)
    {
        var str = Environment.NewLine;
        foreach (var (key, value) in list)
        {
            str += $"{key} {string.Join(",", value)}{Environment.NewLine}";
        }
        return str;
    }
    private void TryStart(RoundStartAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        var availableEasyRecipes = new List<List<string>>();
        var blacklist = new Dictionary<string, List<string>>();
        foreach (var key in BasicReagentList)
        {
            blacklist.Add(key, new List<string>());
        }
        foreach (var key in BasicReagentList)
        {
            foreach (var otherkey in BasicReagentList)
            {
                if (key == otherkey)
                    continue;
                if (blacklist[key].Contains(otherkey))
                    continue;

                availableEasyRecipes.Add(new List<string>() { key, otherkey });
                blacklist[otherkey].Add(key);
            }
        }
        if (!TryRandomize(EasyPotionList.Clone(), availableEasyRecipes, BasicCan, 26, out var easyPotionRecipes, out var easyPotions))
        {
            Log.Debug("Failure while generating easy potion recipes");
            args.Cancel();
            return;
        }
        var availableMediumRecipes = new List<List<string>>();
        foreach (var key in easyPotions)
        {
            foreach (var easyrecipe in availableEasyRecipes)
            {
                var result = new List<string>() {
                    key
                };
                result.AddRange(easyrecipe);
                availableMediumRecipes.Add(result);
            }
        }
        if (!TryRandomize(MediumPotionList.Clone(), availableMediumRecipes, BasicCan, 8, out var mediumPotionRecipes, out var mediumPotions))
        {
            Log.Debug("Failure while generating medium potion recipes");
            args.Cancel();
            return;
        }
        var availableHardRecipes = new List<List<string>>();
        foreach (var key in mediumPotions)
        {
            foreach (var key2 in easyPotions)
            {
                foreach (var key3 in BasicReagentList)
                {
                    availableHardRecipes.Add(new List<string>() {
                        key,
                        key2,
                        key3
                    });
                }
            }
        }
        if (!TryRandomize(HardPotionList.Clone(), availableHardRecipes, BasicCan, 1, out var hardPotionRecipes, out var hardPotions))
        {
            Log.Debug("Failure while generating hard potion recipes");
            args.Cancel();
            return;
        }
        Log.Warning($"easy potion list {GetString(easyPotionRecipes)}");
        Log.Warning($"medium potion list {GetString(mediumPotionRecipes)}");
        Log.Warning($"hard potion list {GetString(hardPotionRecipes)}");
        //_prototype.LoadString
        var allSelectedPotions = new List<string>();
        allSelectedPotions.AddRange(easyPotions);
        allSelectedPotions.AddRange(mediumPotions);
        allSelectedPotions.AddRange(hardPotions);
        var allPotions = new List<string>();
        allPotions.AddRange(EasyPotionList);
        allPotions.AddRange(MediumPotionList);
        allPotions.AddRange(HardPotionList);
        var allRecipes = new Dictionary<string, List<string>>();
        foreach (var pair in easyPotionRecipes)
            allRecipes.Add(pair.Key, pair.Value);

        foreach (var pair in mediumPotionRecipes)
            allRecipes.Add(pair.Key, pair.Value);

        foreach (var pair in hardPotionRecipes)
            allRecipes.Add(pair.Key, pair.Value);

        var newprototypes = string.Empty;
        foreach (var id in allSelectedPotions)
        {
            if (!OriginalReagentPrototypes.TryGetValue(id, out var mapping))
                continue;
            mapping = mapping.Copy();
            mapping.Remove("abstract");
            if (mapping.TryGet<MappingDataNode>("metabolisms", out var metabolism))
            {
                if (metabolism.TryFirstOrNull(out var first))
                {
                    if (first.Value.Value is not MappingDataNode firstnode)
                        continue;
                    if (firstnode.TryGet<SequenceDataNode>("effects", out var node))
                    {
                        node.Add(new MappingDataNode()
                        {
                            Tag = "!type:Jitter",
                            ["conditions"] = new SequenceDataNode() {
                                new MappingDataNode()
                                {
                                    Tag = "!type:ReagentThreshold",
                                    ["min"] = new ValueDataNode("30")
                                }
                            }
                        });
                        node.Add(new MappingDataNode()
                        {
                            Tag = "!type:GenericStatusEffect",
                            ["key"] = new ValueDataNode("Stutter"),
                            ["component"] = new ValueDataNode("StutteringAccent"),
                            ["conditions"] = new SequenceDataNode() {
                                new MappingDataNode()
                                {
                                    Tag = "!type:ReagentThreshold",
                                    ["min"] = new ValueDataNode("30")
                                }
                            }
                        });
                        node.Add(new MappingDataNode()
                        {
                            Tag = "!type:HealthChange",
                            ["damage"] = new MappingDataNode()
                            {
                                {"groups", new MappingDataNode()
                                {
                                    {"Toxin", new ValueDataNode("6")}
                                }}
                            },
                            ["conditions"] = new SequenceDataNode() {
                                new MappingDataNode()
                                {
                                    Tag = "!type:ReagentThreshold",
                                    ["min"] = new ValueDataNode("30")
                                }
                            }
                        });
                    }
                }
            }
            mapping.Get<ValueDataNode>("id").Value = $"{id}clone";
            mapping.Get<ValueDataNode>("color").Value = String.Format("#{0:X6}", _random.Next(0x1000000));
            mapping.Get<ValueDataNode>("physicalDesc").Value = _random.Pick(Descriptions);
            mapping.Get<ValueDataNode>("desc").Value = "";
            mapping.Get<ValueDataNode>("name").Value = Loc.GetString("imperial-medieval-chemistry-basic-name");
            mapping.Get<ValueDataNode>("flavor").Value = _random.Pick(Flavors);
            mapping.Remove("showinbook");
            mapping.Remove("nospawn");
            mapping["showinbook"] = new ValueDataNode("false");
            var result = ConvertMappingToString(mapping);
            if (result == null)
                continue;
            newprototypes = $"{newprototypes}{Environment.NewLine}{result}";
        }
        foreach (var pair in allRecipes)
        {
            var recipe = new MappingDataNode();
            foreach (var key in pair.Value)
            {
                var reagent = key;
                if (allPotions.Contains(reagent))
                    reagent = $"{reagent}clone";

                recipe.Add(reagent, new MappingDataNode() { ["amount"] = new ValueDataNode("1") });
            }
            MappingDataNode? mapping;
            if (!OriginalReactionPrototypes.TryGetValue(pair.Key, out mapping))
            {
                mapping = new MappingDataNode()
                {
                    ["type"] = new ValueDataNode("reaction"),
                    ["id"] = new ValueDataNode(pair.Key),
                    ["reactants"] = recipe,
                    ["products"] = new MappingDataNode()
                    {
                        [$"{pair.Key}clone"] = new ValueDataNode(pair.Value.Count.ToString())
                    },
                };
            }
            else
            {
                mapping = mapping.Copy();
                mapping.Remove("reactants");
                mapping.Remove("products");
                mapping.Remove("abstract");
                mapping["reactants"] = recipe;
                mapping["products"] = new MappingDataNode() { [$"{pair.Key}clone"] = new ValueDataNode(pair.Value.Count.ToString()) };
            }
            var result = ConvertMappingToString(mapping);
            if (result == null)
                continue;
            newprototypes = $"{newprototypes}{Environment.NewLine}{result}";
        }
        foreach (var id in allPotions)
        {
            if (allSelectedPotions.Contains(id))
                continue;
            if (!OriginalReagentPrototypes.TryGetValue(id, out var mapping))
                continue;
            mapping.Get<ValueDataNode>("id").Value = $"{id}clone";
            mapping.Remove("showinbook");
            mapping.Remove("nospawn");
            mapping.Remove("abstract");
            mapping["showinbook"] = new ValueDataNode("false");
            mapping["nospawn"] = new ValueDataNode("true");

            newprototypes = $"{newprototypes}{Environment.NewLine}{ConvertMappingToString(mapping)}";
        }
        if (!_prototype.TryGetKindType("reaction", out var reactionproto))
            return;

        foreach (var id in allPotions)
        {
            if (allRecipes.ContainsKey(id))
                continue;
            if (!_prototype.TryGetMapping(reactionproto, id, out var mapping))
                continue;
            if (!mapping.ContainsKey("abstract"))
                mapping["abstract"] = new ValueDataNode("true");
            newprototypes = $"{newprototypes}{Environment.NewLine}{ConvertMappingToString(mapping)}";
        }
        _uploadManager.SendGamePrototype(newprototypes); // Todo: stop using SendGamePrototype because unoptimized
        foreach (var table in CurrentRecipes)
            table.Value.Clear();
        foreach (var recipe in easyPotionRecipes)
            CurrentRecipes["easy"].Add(recipe.Key, recipe.Value);

        foreach (var recipe in mediumPotionRecipes)
        {
            var list = new List<string>();
            foreach (var key in recipe.Value)
                if (allPotions.Contains(key))
                    list.Add($"{key}clone");
                else
                    list.Add(key);

            CurrentRecipes["medium"].Add(recipe.Key, list);
        }

        foreach (var recipe in hardPotionRecipes)
        {
            var list = new List<string>();
            foreach (var key in recipe.Value)
                if (allPotions.Contains(key))
                    list.Add($"{key}clone");
                else
                    list.Add(key);

            CurrentRecipes["hard"].Add(recipe.Key, list);
        }
        // Log.Debug(newprototypes);
    }
    private string? ConvertMappingToString(MappingDataNode node)
    {
        var data = string.Empty;
        var first = true;
        foreach (var str in node.ToString().Split(Environment.NewLine))
        {
            if (str.Replace(" ", "") == "...")
                continue;

            if (first)
            {
                data += $"- {str}{Environment.NewLine}";
                first = false;
            }
            else
            {
                data += $"  {str}{Environment.NewLine}";
            }
        }
        return data;
    }
}
