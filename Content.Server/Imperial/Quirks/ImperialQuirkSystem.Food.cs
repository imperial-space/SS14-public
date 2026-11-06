using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Quirks;

public sealed partial class ImperialQuirkSystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    /// <summary>GLOB.possible_food_allergies.</summary>
    private static readonly ImperialFoodType[] Allergens =
    [
        ImperialFoodType.Alcohol, ImperialFoodType.Bugs, ImperialFoodType.Dairy, ImperialFoodType.Fruit, ImperialFoodType.Grain,
        ImperialFoodType.Meat, ImperialFoodType.Nuts, ImperialFoodType.Seafood, ImperialFoodType.Sugar, ImperialFoodType.Vegetables,
    ];

    private static readonly Dictionary<ImperialFoodType, string[]> FoodFlavors = new()
    {
        [ImperialFoodType.Meat] = ["meaty", "chicken", "duck", "bacon", "people", "compressed-meat", "meatballs", "falsemeat", "susjerky"],
        [ImperialFoodType.Seafood] = ["fishy", "crabby", "cannedtuna", "fishops"],
        [ImperialFoodType.Dairy] = ["cheesy", "milk", "creamy", "butter", "icecream"],
        [ImperialFoodType.Fruit] = ["fruity", "banana", "apple", "orange", "cherry", "grape", "berry", "watermelon", "pineapple", "lemoon", "raisins"],
        [ImperialFoodType.Vegetables] = ["leafy", "vegetables", "onion", "eggplant", "carrot", "cabbage", "potatoes", "tomato", "corn", "peas", "garlic", "pumpkin"],
        [ImperialFoodType.Grain] = ["bread", "bun", "dough", "sweetdough", "batter", "pasta", "rice", "oats", "muffin"],
        [ImperialFoodType.Sugar] = ["sweet", "syrupy", "honey", "chocolate", "jelly"],
        [ImperialFoodType.Nuts] = ["nutty"],
        [ImperialFoodType.Bugs] = ["bee", "squirming", "cobwebs"],
        [ImperialFoodType.Alcohol] = ["alcohol"],
        [ImperialFoodType.Junkfood] = ["cheap", "nutribrick", "boritos", "nachos", "donk", "syndicakes", "cheapnoodles", "trashy"],
        [ImperialFoodType.Pineapple] = ["pineapple", "pinacolada"],
        [ImperialFoodType.Gross] = ["terrible", "horrible", "trashy", "slimy", "bad-joke"],
    };

    private static readonly Dictionary<ImperialFoodType, string[]> FoodTags = new()
    {
        [ImperialFoodType.Meat] = ["Meat", "Cutlet", "Steak"],
        [ImperialFoodType.Seafood] = ["CubanCarp"],
        [ImperialFoodType.Fruit] = ["Fruit"],
        [ImperialFoodType.Vegetables] = ["Vegetable", "Carrot", "Potato", "Corn"],
        [ImperialFoodType.Grain] = ["Bread", "Wheat"],
        [ImperialFoodType.Junkfood] = ["DonkPocket"],
        [ImperialFoodType.Raw] = ["Raw"],
    };

    private static readonly Dictionary<ImperialFoodType, string[]> FoodReagents = new()
    {
        [ImperialFoodType.Meat] = ["Protein"],
        [ImperialFoodType.Dairy] = ["Milk", "Cream"],
        [ImperialFoodType.Sugar] = ["Sugar"],
        [ImperialFoodType.Alcohol] = ["Ethanol"],
    };

    private static readonly EntProtoId Paper2 = "Paper";

    private const float AllergicIngestAmount = 10f;
    private const float AllergicIngestChance = 0.3f;

    private void InitializeFood()
    {
        SubscribeLocalEvent<ImperialQuirkFoodAllergyComponent, ImperialQuirkAddedEvent>(OnFoodAllergyAdded);
        SubscribeLocalEvent<ImperialQuirkFoodAllergyComponent, IngestingEvent>(OnAllergicIngest);
        SubscribeLocalEvent<ImperialQuirkTasteComponent, IngestingEvent>(OnTasteIngest);
        SubscribeLocalEvent<ImperialQuirkVoraciousComponent, BeforeIngestedEvent>(OnVoraciousBite);
    }

    /// <summary>Тип пищи по тегам, вкусам блюда и реагентам откушенного куска.</summary>
    public ImperialFoodType GetFoodType(EntityUid food, Solution? split)
    {
        var result = ImperialFoodType.None;
        var flavors = new HashSet<string>();
        if (TryComp<FlavorProfileComponent>(food, out var profile))
            flavors.UnionWith(profile.Flavors);

        if (split != null)
        {
            foreach (var (reagent, _) in split.Contents)
            {
                if (_proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(reagent.Prototype, out var proto))
                    if (proto.Flavor != null)
                        flavors.Add(proto.Flavor);

                foreach (var (type, ids) in FoodReagents)
                {
                    if (ids.Contains(reagent.Prototype))
                        result |= type;
                }
            }
        }

        foreach (var (type, list) in FoodFlavors)
        {
            if (list.Any(flavors.Contains))
                result |= type;
        }

        foreach (var (type, tags) in FoodTags)
        {
            if (tags.Any(t => _tag.HasTag(food, t)))
                result |= type;
        }

        if (MetaData(food).EntityPrototype?.ID.Contains("Pineapple") == true)
            result |= ImperialFoodType.Pineapple;

        return result;
    }

    private static string FoodTypeName(ImperialFoodType type) => $"imperial-quirk-food-{type.ToString().ToLowerInvariant()}";

    private void OnFoodAllergyAdded(Entity<ImperialQuirkFoodAllergyComponent> ent, ref ImperialQuirkAddedEvent args)
    {
        ent.Comp.Allergy = _random.Pick(Allergens);
        var name = Loc.GetString(FoodTypeName(ent.Comp.Allergy));
        Message(args.Player, Loc.GetString("imperial-quirk-food-allergy-gain", ("food", name)));

        var tag = Spawn(Paper2, Transform(ent).Coordinates);
        _metaData.SetEntityName(tag, Loc.GetString("imperial-quirk-allergy-tag-name"));
        if (TryComp<Content.Shared.Paper.PaperComponent>(tag, out var paper))
            _paper.SetContent((tag, paper), Loc.GetString("imperial-quirk-allergy-tag", ("name", Name(ent)), ("list", name)));
        GiveItem(ent, tag);
    }

    /// <summary>Аллергическая реакция на пищу: гистамин, отёк и тошнота.</summary>
    private void OnAllergicIngest(Entity<ImperialQuirkFoodAllergyComponent> ent, ref IngestingEvent args)
    {
        if ((GetFoodType(args.Food, args.Split) & ent.Comp.Allergy) == 0)
            return;

        _popup.PopupEntity(Loc.GetString("imperial-quirk-food-allergy-reaction"), ent, ent, PopupType.LargeCaution);
        AddReagent(ent, Histamine, AllergicIngestAmount);
        if (!HasComp<ImperialQuirkStrongStomachComponent>(ent) && _random.Prob(AllergicIngestChance))
            _vomit.Vomit(ent);
    }

    /// <summary>
    /// Реакция языка: любимое — приятно, нелюбимое — отвращение и, бывает, рвота.
    /// Настроения в SS14 нет, поэтому остаются сообщения и тошнота.
    /// </summary>
    private void OnTasteIngest(Entity<ImperialQuirkTasteComponent> ent, ref IngestingEvent args)
    {
        if (HasComp<ImperialQuirkAgeusiaComponent>(ent) || _timing.CurTime < ent.Comp.NextReaction)
            return;

        var type = GetFoodType(args.Food, args.Split);
        if ((type & ent.Comp.Disliked) != 0)
        {
            ent.Comp.NextReaction = _timing.CurTime + TimeSpan.FromSeconds(10);
            _popup.PopupEntity(Loc.GetString("imperial-quirk-food-disgusting"), ent, ent, PopupType.MediumCaution);
            if (!HasComp<ImperialQuirkStrongStomachComponent>(ent) && _random.Prob(0.25f))
                _vomit.Vomit(ent);
            return;
        }

        if ((type & ent.Comp.Liked) == 0)
            return;

        ent.Comp.NextReaction = _timing.CurTime + TimeSpan.FromSeconds(10);
        _popup.PopupEntity(Loc.GetString("imperial-quirk-food-delicious"), ent, ent);
    }

    /// <summary>Причуды, меняющие вкусы языка: (любит, не любит, поменять местами).</summary>
    private static readonly Dictionary<string, (ImperialFoodType Liked, ImperialFoodType Disliked, bool Swap)> TasteQuirks = new()
    {
        ["ImperialQuirkVegetarian"] = (ImperialFoodType.None, ImperialFoodType.Meat, false),
        ["ImperialQuirkPineappleHater"] = (ImperialFoodType.None, ImperialFoodType.Pineapple, false),
        ["ImperialQuirkPineappleLiker"] = (ImperialFoodType.Pineapple, ImperialFoodType.None, false),
        ["ImperialQuirkDeviantTastes"] = (ImperialFoodType.None, ImperialFoodType.None, true),
    };

    private void ApplyTaste(EntityUid mob, string quirk)
    {
        if (!TasteQuirks.TryGetValue(quirk, out var mod))
            return;

        var taste = EnsureComp<ImperialQuirkTasteComponent>(mob);
        if (mod.Swap)
            (taste.Liked, taste.Disliked) = (taste.Disliked, taste.Liked);

        taste.Liked = (taste.Liked | mod.Liked) & ~mod.Disliked;
        taste.Disliked = (taste.Disliked | mod.Disliked) & ~mod.Liked;
    }

    private void OnVoraciousBite(Entity<ImperialQuirkVoraciousComponent> ent, ref BeforeIngestedEvent args)
    {
        args.Transfer *= 2;
    }
}
