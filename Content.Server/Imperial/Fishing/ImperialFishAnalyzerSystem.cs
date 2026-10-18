using Content.Server.Imperial.Research;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Анализатор рыб: при использовании на рыбе или аквариуме показывает данные (ui_data FishAnalyzer)
/// и засчитывает рыбу в эксперимент сканирования рыб.
/// </summary>
public sealed class ImperialFishAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly ImperialAquariumSystem _aquarium = default!;
    [Dependency] private readonly TgExperimentSystem _experiments = default!;

    private static readonly string[] FishExperiments = { "TgExpFish", "TgExpFishSecond" };

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFishAnalyzerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialFishAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnMapInit(Entity<ImperialFishAnalyzerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CaseColor = new Color(_random.Next(16, 256), _random.Next(16, 256), _random.Next(16, 256));
        _appearance.SetData(ent, ImperialFishAnalyzerVisuals.CaseColor, ent.Comp.CaseColor);
        Dirty(ent);
    }

    private void OnAfterInteract(Entity<ImperialFishAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<ImperialFishComponent>(target) && !HasComp<ImperialAquariumComponent>(target))
            return;

        args.Handled = true;
        if (HasComp<ImperialFishComponent>(target))
            _experiments.ScanFor(ent, args.User, target, FishExperiments);

        ent.Comp.Scanned = target;
        _ui.OpenUi(ent.Owner, ImperialFishAnalyzerUiKey.Key, args.User);
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0;
        var query = EntityQueryEnumerator<ImperialFishAnalyzerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Scanned != null && _ui.IsUiOpen(uid, ImperialFishAnalyzerUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    private void UpdateUi(Entity<ImperialFishAnalyzerComponent> ent)
    {
        var entries = new List<ImperialFishAnalyzerEntry>();
        var single = false;
        if (ent.Comp.Scanned is { } scanned && !TerminatingOrDeleted(scanned))
        {
            if (TryComp<ImperialFishComponent>(scanned, out var fish))
            {
                single = true;
                entries.Add(Describe((scanned, fish)));
            }
            else
            {
                foreach (var inside in _aquarium.GetFishes(scanned))
                    entries.Add(Describe(inside));
            }
        }

        _ui.SetUiState(ent.Owner, ImperialFishAnalyzerUiKey.Key, new ImperialFishAnalyzerBoundUserInterfaceState(entries, single));
    }

    /// <summary>extract_fish_info().</summary>
    private ImperialFishAnalyzerEntry Describe(Entity<ImperialFishComponent> ent)
    {
        var fish = ent.Comp;
        var entry = new ImperialFishAnalyzerEntry
        {
            Name = Name(ent),
            Proto = Prototype(ent)?.ID,
            Health = fish.Dead ? 0 : MathF.Round(ImperialFishSystem.GetHealthPercentage(fish) * 100),
            Size = fish.Size,
            Weight = fish.Weight,
            MinTemp = fish.TemperatureMin,
            MaxTemp = fish.TemperatureMax,
            Hunger = 1 - _fish.GetHunger(fish),
            FluidCompatible = fish.SafeFluid,
            Fluid = Loc.GetString($"imperial-aquarium-fluid-{fish.RequiredFluid.ToString().ToLowerInvariant()}"),
            SuitableTemp = fish.SafeTemperature,
            BreedTimer = Math.Max(0, (int) (fish.BreedingWait - _timing.CurTime).TotalSeconds),
        };

        if (_proto.TryIndex<ReagentPrototype>(fish.Food, out var reagent))
        {
            entry.Food = reagent.LocalizedName;
            entry.FoodColor = reagent.SubstanceColor;
        }
        else
        {
            entry.Food = fish.Food.Id;
        }

        foreach (var traitId in fish.Traits)
        {
            if (!_proto.TryIndex(traitId, out var trait))
                continue;

            entry.Traits.Add(new ImperialFishAnalyzerTrait
            {
                Name = Loc.GetString(trait.Name),
                Description = Loc.GetString(trait.Description),
                Inheritability = trait.Inheritability,
            });
        }

        foreach (var evolutionId in fish.EvolutionTypes)
        {
            if (!_proto.TryIndex<ImperialFishEvolutionPrototype>(evolutionId, out var evolution))
                continue;

            entry.Evolutions.Add(new ImperialFishAnalyzerEvolution
            {
                Name = EvolutionName(evolution),
                Proto = evolution.NewFish,
                Probability = evolution.Probability,
                Conditions = EvolutionTooltip(evolution),
            });
        }

        return entry;
    }

    public string EvolutionName(ImperialFishEvolutionPrototype evolution)
    {
        if (evolution.Name is { } locName)
            return Loc.GetString(locName);

        return _proto.TryIndex<EntityPrototype>(evolution.NewFish, out var newFish) ? newFish.Name : evolution.NewFish.Id;
    }

    /// <summary>get_evolution_tooltip().</summary>
    public string EvolutionTooltip(ImperialFishEvolutionPrototype evolution)
    {
        var result = string.Empty;
        if (evolution.TemperatureMin > 0 || !float.IsPositiveInfinity(evolution.TemperatureMax))
        {
            if (evolution.TemperatureMin <= 0)
                result = Loc.GetString("imperial-fish-evolution-temp-below", ("max", evolution.TemperatureMax));
            else if (float.IsPositiveInfinity(evolution.TemperatureMax))
                result = Loc.GetString("imperial-fish-evolution-temp-above", ("min", evolution.TemperatureMin));
            else
                result = Loc.GetString("imperial-fish-evolution-temp-range", ("min", evolution.TemperatureMin), ("max", evolution.TemperatureMax));
        }

        if (evolution.ConditionsNote is { } note)
            result += " " + Loc.GetString(note);

        return result.Trim();
    }
}
