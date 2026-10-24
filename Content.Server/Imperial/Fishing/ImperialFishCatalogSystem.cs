using System.Linq;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>Энциклопедия рыб: где ловится каждая рыба, что любит и не любит, черты и сложность (ui_static_data FishCatalog).</summary>
public sealed class ImperialFishCatalogSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private List<ImperialFishCatalogEntry>? _entries;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFishCatalogComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _entries = null);
    }

    private void OnUseInHand(Entity<ImperialFishCatalogComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.OpenUi(ent.Owner, ImperialFishCatalogUiKey.Key, args.User);
        _ui.SetUiState(ent.Owner, ImperialFishCatalogUiKey.Key, new ImperialFishCatalogBoundUserInterfaceState(GetEntries()));
    }

    public List<ImperialFishCatalogEntry> GetEntries()
    {
        if (_entries != null)
            return _entries;

        _entries = new List<ImperialFishCatalogEntry>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>().OrderBy(p => p.Name))
        {
            if (proto.Abstract || !proto.TryGetComponent<ImperialFishComponent>(out var fish, EntityManager.ComponentFactory) || !fish.ShowInCatalog)
                continue;

            _entries.Add(new ImperialFishCatalogEntry
            {
                Prototype = proto.ID,
                Name = proto.Name,
                Description = proto.Description,
            });
            FillEntry(_entries[^1], proto.ID, fish);
        }

        return _entries;
    }

    /// <summary>ui_static_data() + build_fishing_tips().</summary>
    private void FillEntry(ImperialFishCatalogEntry entry, string id, ImperialFishComponent fish)
    {
        entry.Fluid = Loc.GetString($"imperial-aquarium-fluid-{fish.RequiredFluid.ToString().ToLowerInvariant()}");
        entry.TempMin = fish.TemperatureMin;
        entry.TempMax = fish.TemperatureMax;
        entry.Feed = fish.Food == "Nutriment"
            ? Loc.GetString("imperial-fish-catalog-company-feed")
            : _proto.TryIndex<ReagentPrototype>(fish.Food, out var reagent) ? reagent.LocalizedName : fish.Food.Id;
        entry.Source = Loc.GetString(fish.RandomCaseRarity > 0 ? "imperial-fish-catalog-source-packs" : "imperial-fish-catalog-source-unknown");
        entry.Size = fish.AverageSize;
        entry.Weight = fish.AverageWeight;
        entry.Beauty = fish.Beauty;

        var spots = new List<string>();
        foreach (var src in _proto.EnumeratePrototypes<ImperialFishSourcePrototype>())
        {
            if (src.CatalogDescription is { } description && src.Table.ContainsKey(id))
                spots.Add(Loc.GetString(description));
        }

        entry.Tips.Spots = spots.Count > 0 ? string.Join(", ", spots.Distinct()) : Loc.GetString("imperial-fish-catalog-unknown");
        entry.Tips.FavoriteBait = BaitList(fish.FavoriteBait);
        entry.Tips.DislikedBait = BaitList(fish.DislikedBait);

        var difficulty = fish.DifficultyModifier;
        foreach (var traitId in fish.InitialTraits)
        {
            if (!_proto.TryIndex(traitId, out var trait))
                continue;

            difficulty += trait.AddedDifficulty;
            entry.Tips.Traits.Add(Loc.GetString(trait.Description));
        }

        if (entry.Tips.Traits.Count == 0)
            entry.Tips.Traits.Add(Loc.GetString("imperial-fish-catalog-no-traits"));

        entry.Tips.Difficulty = Loc.GetString(difficulty switch
        {
            < 10 => "imperial-fish-catalog-difficulty-easy",
            < 20 => "imperial-fish-catalog-difficulty-medium",
            _ => "imperial-fish-catalog-difficulty-hard",
        });
    }

    /// <summary>bait_description().</summary>
    private string BaitList(List<ImperialBaitIdentifier> baits)
    {
        if (baits.Count == 0)
            return Loc.GetString("imperial-fish-catalog-none");

        var result = new List<string>();
        foreach (var bait in baits)
        {
            if (bait.Proto is { } proto)
            {
                result.Add(_proto.TryIndex<EntityPrototype>(proto, out var entity) ? entity.Name : proto.Id);
            }
            else if (bait.FoodTypes != ImperialFoodTypes.None)
            {
                var flags = Enum.GetValues<ImperialFoodTypes>()
                    .Where(f => f != ImperialFoodTypes.None && bait.FoodTypes.HasFlag(f))
                    .Select(f => Loc.GetString($"imperial-food-type-{f.ToString().ToLowerInvariant()}"));
                result.Add(string.Join(", ", flags));
            }
            else if (bait.Reagent is { } reagentId)
            {
                var name = _proto.TryIndex<ReagentPrototype>(reagentId, out var reagent) ? reagent.LocalizedName : reagentId.Id;
                result.Add(Loc.GetString("imperial-fish-catalog-reagent-bait", ("reagent", name), ("amount", bait.Amount)));
            }
            else
            {
                result.Add(Loc.GetString("imperial-fish-catalog-special-bait"));
            }
        }

        return string.Join(", ", result);
    }
}
