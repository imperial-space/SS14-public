using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Nutrition.Components;
using Content.Shared.PowerCell.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Наживка: что ею может быть, качество, типы пищи и предпочтения рыб
/// (check_bait, is_matching_bait, /obj/item/fishing_lure/is_catchable_fish).
/// </summary>
public sealed class ImperialFishingBaitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;

    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";
    private static readonly ProtoId<TagPrototype> RawTag = "Raw";
    private static readonly ProtoId<TagPrototype> FruitTag = "Fruit";
    private static readonly ProtoId<TagPrototype> VegetableTag = "Vegetable";
    private static readonly ProtoId<TagPrototype> DonutTag = "Donut";
    private static readonly ProtoId<TagPrototype> BreadTag = "Bread";

    /// <summary>Прототип и его родители.</summary>
    public bool IsOrInherits(EntityUid uid, string protoId)
    {
        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        return IsOrInherits(proto.ID, protoId);
    }

    public bool IsOrInherits(string childId, string protoId)
    {
        if (childId == protoId)
            return true;

        return _proto.HasIndex<EntityPrototype>(childId)
               && _proto.EnumerateParents<EntityPrototype>(childId).Any(p => p.ID == protoId);
    }

    /// <summary>TRAIT_FISHING_BAIT.</summary>
    public bool IsBait(EntityUid uid)
    {
        return HasComp<ImperialFishingBaitComponent>(uid) || HasComp<EdibleComponent>(uid) || HasComp<ImperialFishComponent>(uid)
               || HasComp<CashComponent>(uid) || HasComp<PowerCellComponent>(uid);
    }

    public ImperialBaitQuality GetQuality(EntityUid uid)
    {
        if (TryComp<ImperialFishingBaitComponent>(uid, out var bait) && bait.Quality != ImperialBaitQuality.None)
            return bait.Quality;

        if (TryComp<ImperialFishComponent>(uid, out var fish))
            return fish.BaitQuality;

        return ImperialBaitQuality.None;
    }

    /// <summary>TRAIT_BAIT_ALLOW_FISHING_DUD: деньги и блёсны не убирают «пустой» улов.</summary>
    public bool AllowsDud(EntityUid uid)
    {
        return CompOrNull<ImperialFishingBaitComponent>(uid)?.AllowDud == true || HasComp<CashComponent>(uid);
    }

    public bool IsOmni(EntityUid uid) => CompOrNull<ImperialFishingBaitComponent>(uid)?.Omni == true;

    public bool IsLure(EntityUid uid) => CompOrNull<ImperialFishingBaitComponent>(uid)?.Lure is { } lure && lure != ImperialLureKind.None;

    /// <summary>get_item_credit_value (для любителей блестящего).</summary>
    public int GetCreditValue(EntityUid uid)
    {
        if (!HasComp<CashComponent>(uid))
            return 0;

        return TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
    }

    /// <summary>Типы пищи TG для предмета (foodtypes).</summary>
    public ImperialFoodTypes GetFoodTypes(EntityUid uid)
    {
        var types = CompOrNull<ImperialFishingBaitComponent>(uid)?.FoodTypes ?? ImperialFoodTypes.None;

        if (HasComp<ImperialFishComponent>(uid))
            types |= ImperialFoodTypes.Seafood | ImperialFoodTypes.Meat | ImperialFoodTypes.Raw | ImperialFoodTypes.Gore;

        if (!HasComp<EdibleComponent>(uid))
            return types;

        if (_tag.HasTag(uid, MeatTag))
            types |= ImperialFoodTypes.Meat;
        if (_tag.HasTag(uid, RawTag))
            types |= ImperialFoodTypes.Raw;
        if (_tag.HasTag(uid, FruitTag))
            types |= ImperialFoodTypes.Fruit;
        if (_tag.HasTag(uid, VegetableTag))
            types |= ImperialFoodTypes.Vegetables;
        if (_tag.HasTag(uid, DonutTag))
            types |= ImperialFoodTypes.Junkfood | ImperialFoodTypes.Sugar | ImperialFoodTypes.Fried | ImperialFoodTypes.Breakfast;
        if (_tag.HasTag(uid, BreadTag) || IsOrInherits(uid, "FoodBreadBase"))
            types |= ImperialFoodTypes.Grain;
        if (IsOrInherits(uid, "FoodSnackBase"))
            types |= ImperialFoodTypes.Junkfood;
        if (IsOrInherits(uid, "FoodMeatFish") || IsOrInherits(uid, "FoodMeatFishRaw"))
            types |= ImperialFoodTypes.Seafood | ImperialFoodTypes.Meat;
        if (HasComp<ProduceComponent>(uid) && (types & (ImperialFoodTypes.Fruit | ImperialFoodTypes.Vegetables)) == 0)
            types |= ImperialFoodTypes.Vegetables;

        return types;
    }

    /// <summary>is_matching_bait().</summary>
    public bool Matches(EntityUid bait, ImperialBaitIdentifier identifier)
    {
        if (identifier.Special == "lure")
            return IsLure(bait);
        if (identifier.Special == "fish")
            return HasComp<ImperialFishComponent>(bait);

        if (identifier.Proto is { } proto)
            return IsOrInherits(bait, proto);

        if (identifier.FoodTypes != ImperialFoodTypes.None)
            return (GetFoodTypes(bait) & identifier.FoodTypes) != 0;

        if (identifier.Reagent is { } reagent)
            return HasReagent(bait, reagent, identifier.Amount);

        return false;
    }

    private bool HasReagent(EntityUid uid, string reagent, float amount)
    {
        foreach (var (name, _) in _solution.EnumerateSolutions(uid))
        {
            if (name != null && _solution.TryGetSolution(uid, name, out _, out var solution) && solution.GetTotalPrototypeQuantity(reagent) >= amount)
                return true;
        }

        return false;
    }

    /// <summary>check_bait(): множитель шанса поймать рыбу на эту наживку.</summary>
    public float CheckBait(EntityUid bait, string fishProto, ImperialFishComponent? fishInstance = null)
    {
        if (CompOrNull<ImperialFishingBaitComponent>(bait) is { Lure: not ImperialLureKind.None } lure)
            return CheckLure(bait, lure, fishProto, fishInstance);

        if (IsOmni(bait))
            return 1;

        var fish = fishInstance ?? _data.GetFish(fishProto);
        if (fish == null)
            return 1;

        var multiplier = 1f;
        foreach (var id in fish.FavoriteBait)
        {
            if (Matches(bait, id))
                multiplier *= 2;
        }

        foreach (var id in fish.DislikedBait)
        {
            if (Matches(bait, id))
                multiplier *= 0.5f;
        }

        return multiplier;
    }

    /// <summary>/obj/item/fishing_lure/check_bait.</summary>
    private float CheckLure(EntityUid bait, ImperialFishingBaitComponent lure, string fishProto, ImperialFishComponent? fishInstance)
    {
        var fish = fishInstance ?? _data.GetFish(fishProto);
        if (fish == null)
            return 0;

        var multiplier = 0f;
        if (fish.FavoriteBait.Any(id => id.Special == "lure"))
            multiplier += 2;

        if (IsCatchableByLure(lure.Lure, fishProto, fish, fishInstance != null))
            multiplier += 10;

        return multiplier;
    }

    /// <summary>is_catchable_fish() каждой блесны.</summary>
    public bool IsCatchableByLure(ImperialLureKind lure, string fishProto, ImperialFishComponent fish, bool instance)
    {
        var size = instance ? fish.Size : fish.AverageSize;
        var traits = (instance ? fish.Traits : fish.InitialTraits).Select(t => t.Id).ToHashSet();
        foreach (var spontaneous in _data.GetSpontaneousTraits(fishProto))
            traits.Add(spontaneous);

        bool HasAny(params string[] ids) => ids.Any(traits.Contains);

        var fluid = fish.RequiredFluid;
        var amphibiousAir = traits.Contains("Amphibious") && fluid == ImperialFishFluid.Air;
        var freshish = fluid is ImperialFishFluid.Freshwater or ImperialFishFluid.Anadromous or ImperialFishFluid.AnyWater;

        switch (lure)
        {
            case ImperialLureKind.Minnow:
            {
                var intermediate = ImperialFishing.SizeSmallMax + (ImperialFishing.SizeNormalMax - ImperialFishing.SizeSmallMax);
                return size >= ImperialFishing.SizeTinyMax * 0.5f && size <= intermediate && !HasAny("Vegan", "PickyEater", "Nocturnal", "Heavy");
            }
            case ImperialLureKind.Plug:
                return size > ImperialFishing.SizeSmallMax && !HasAny("Vegan", "PickyEater", "Nocturnal", "Heavy");
            case ImperialLureKind.Dropping:
            {
                foreach (var sourceId in new[] { "ImperialFishSourceToilet", "ImperialFishSourceMoistureTrap" })
                {
                    if (_proto.TryIndex<ImperialFishSourcePrototype>(sourceId, out var source) && source.Table.ContainsKey(fishProto))
                        return true;
                }

                if (fish.FavoriteBait.Any(id => (id.FoodTypes & (ImperialFoodTypes.Junkfood | ImperialFoodTypes.Gross | ImperialFoodTypes.Toxic)) != 0))
                    return true;

                return fish.Beauty <= -500;
            }
            case ImperialLureKind.Spoon:
                return size >= ImperialFishing.SizeTinyMax + 1 && size <= ImperialFishing.SizeNormalMax
                       && !HasAny("Vegan", "PickyEater", "Nocturnal", "Heavy") && (freshish || amphibiousAir);
            case ImperialLureKind.ArtificialFly:
                return traits.Contains("PickyEater");
            case ImperialLureKind.Led:
                return HasAny("Nocturnal", "Heavy");
            case ImperialLureKind.LuckyCoin:
                return traits.Contains("ShinyLover");
            case ImperialLureKind.Algae:
                return traits.Contains("Vegan");
            case ImperialLureKind.Grub:
                return size < ImperialFishing.SizeSmallMax && !HasAny("Vegan", "PickyEater");
            case ImperialLureKind.Buzzbait:
                return traits.Contains("Electrogenesis");
            case ImperialLureKind.Spinnerbait:
                return traits.Contains("Predator") && (freshish || amphibiousAir);
            case ImperialLureKind.DaisyChain:
                return traits.Contains("Predator") && fluid is ImperialFishFluid.Saltwater or ImperialFishFluid.Anadromous or ImperialFishFluid.AnyWater;
            default:
                return true;
        }
    }
}
