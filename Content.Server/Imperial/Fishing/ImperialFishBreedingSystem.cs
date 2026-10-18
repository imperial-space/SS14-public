using System.Linq;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Fishing;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Кормление, размножение, наследование черт и эволюции рыб (feed, try_to_reproduce, create_offspring, inherit_traits, fish_evolution).
/// </summary>
public sealed class ImperialFishBreedingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly ImperialFishTraitSystem _traits = default!;
    [Dependency] private readonly ImperialFishDataSystem _data = default!;

    /// <summary>OFFSPRING_FISH_BREEDING_TIMEOUT_MULT.</summary>
    private const int OffspringBreedingTimeoutMult = 2;

    private static readonly FixedPoint2 FeedAmount = FixedPoint2.New(0.1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialAquariumComponent, ImperialFishTryReproduceEvent>(OnTryReproduce);
    }

    #region Состояния

    public bool FedLube(ImperialFishComponent fish) => _timing.CurTime < fish.FedLubeUntil;

    public bool Mutagenic(ImperialFishComponent fish) => _timing.CurTime < fish.MutagenicUntil;

    /// <summary>TRAIT_FISH_MADE_OF_BONE: костерыба и мастодонт.</summary>
    public static bool MadeOfBone(ImperialFishComponent fish) => fish.FishId is "boned" or "mastodon";

    /// <summary>TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH.</summary>
    public bool GrowthStopped(EntityUid aquarium)
    {
        return CompOrNull<ImperialAquariumComponent>(aquarium)?.StopGrowth == true;
    }

    #endregion

    #region Кормление

    /// <summary>feed(): рыба съедает по 0.1 ед. реагентов с особым действием, затем свою еду (или что попало).</summary>
    public void Feed(Entity<ImperialFishComponent> ent, Entity<SolutionComponent> solutionEnt)
    {
        var fish = ent.Comp;
        if (fish.Dead)
            return;

        var solution = solutionEnt.Comp.Solution;
        var alreadyFed = false;
        var wasHungry = _fish.GetHunger(fish) > 0;

        foreach (var reagent in solution.Contents.ToList())
        {
            var id = reagent.Reagent.Prototype;
            if (reagent.Quantity < FeedAmount || !UsedOnFish(ent, id))
                continue;

            _solution.RemoveReagent(solutionEnt, id, FeedAmount);
            if (id == fish.Food)
                alreadyFed = true;
        }

        if (wasHungry && _fish.GetHunger(fish) <= 0)
            return;

        if (alreadyFed)
        {
            _fish.SateHunger(ent);
            return;
        }

        if (_solution.RemoveReagent(solutionEnt, fish.Food.Id, FeedAmount) > 0)
        {
            _fish.SateHunger(ent);
            return;
        }

        if (solution.Contents.Count == 0)
            return;

        var wrong = _random.Pick(solution.Contents);
        _solution.RemoveReagent(solutionEnt, wrong.Reagent.Prototype, FeedAmount);
    }

    /// <summary>used_on_fish(): особое действие реагентов.</summary>
    private bool UsedOnFish(Entity<ImperialFishComponent> ent, string reagent)
    {
        var fish = ent.Comp;
        switch (reagent)
        {
            case "SpaceLube":
                fish.FedLubeUntil = _timing.CurTime + fish.FeedingFrequency;
                return true;
            case "UnstableMutagen":
                fish.MutagenicUntil = _timing.CurTime + fish.FeedingFrequency * 0.8;
                return true;
            case "Milk":
                if (!MadeOfBone(fish))
                    return false;
                _fish.RepairFish(ent, fish.MaxIntegrity * MathF.Max(_fish.GetHunger(fish) * 0.5f, 0.12f));
                _fish.SateHunger(ent);
                return true;
        }

        return false;
    }

    #endregion

    #region Размножение

    /// <summary>ready_to_reproduce().</summary>
    public bool ReadyToReproduce(Entity<ImperialFishComponent> ent, bool beingTargeted = false)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container) || !HasComp<ImperialAquariumComponent>(container.Owner))
            return false;
        if (beingTargeted && _traits.Has(ent.Comp, "NoMating"))
            return false;
        if (!beingTargeted && GetAquariumFishes(container.Owner).Count >= ImperialFishing.AquariumMaxBreedingPopulation)
            return false;

        return !GrowthStopped(container.Owner)
            && ImperialFishSystem.GetHealthPercentage(ent.Comp) >= 0.8f
            && _timing.CurTime >= ent.Comp.BreedingWait;
    }

    public List<Entity<ImperialFishComponent>> GetAquariumFishes(EntityUid aquarium)
    {
        var result = new List<Entity<ImperialFishComponent>>();
        if (!_container.TryGetContainer(aquarium, ImperialAquariumComponent.ContainerId, out var container))
            return result;

        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<ImperialFishComponent>(uid, out var comp))
                result.Add((uid, comp));
        }

        return result;
    }

    private void OnTryReproduce(Entity<ImperialAquariumComponent> aquarium, ref ImperialFishTryReproduceEvent args)
    {
        if (ReadyToReproduce(args.Fish))
            TryToReproduce(args.Fish, aquarium);
    }

    /// <summary>get_candidates() аквариума: рыбы, с которыми можно размножаться.</summary>
    private List<Entity<ImperialFishComponent>> GetCandidates(Entity<ImperialFishComponent> fish, EntityUid aquarium)
    {
        var byType = GetAquariumFishes(aquarium).GroupBy(f => Prototype(f)?.ID ?? string.Empty);
        var ownType = Prototype(fish)?.ID ?? string.Empty;
        var crossbreeder = _traits.Has(fish.Comp, "Crossbreeder");
        var candidates = new List<Entity<ImperialFishComponent>>();
        foreach (var group in byType)
        {
            if (!crossbreeder && group.Key != ownType && !fish.Comp.CompatibleTypes.Any(t => t.Id == group.Key))
                continue;

            var list = group.ToList();
            if (list.Count >= list[0].Comp.StablePopulation)
                continue;

            candidates.AddRange(list);
        }

        return candidates;
    }

    /// <summary>try_to_reproduce().</summary>
    public bool TryToReproduce(Entity<ImperialFishComponent> ent, EntityUid aquarium)
    {
        var fish = ent.Comp;
        Entity<ImperialFishComponent>? second = null;

        if (!_traits.Has(fish, "NoMating"))
        {
            var available = GetCandidates(ent, aquarium).Where(f => f.Owner != ent.Owner).ToList();
            _random.Shuffle(available);
            foreach (var other in available)
            {
                if (!ReadyToReproduce(other, true))
                    continue;

                second = other;
                break;
            }
        }

        if (second == null && !_traits.Has(fish, "Parthenogenesis"))
            return false;

        var possible = new List<ImperialFishEvolutionPrototype>();
        foreach (var evolutionId in fish.EvolutionTypes)
        {
            if (_proto.TryIndex<ImperialFishEvolutionPrototype>(evolutionId, out var evolution)
                && CheckEvolution(evolution, ent, second, aquarium))
                possible.Add(evolution);
        }

        if (second is { } mate)
        {
            foreach (var evolutionId in mate.Comp.EvolutionTypes.Except(fish.EvolutionTypes))
            {
                if (_proto.TryIndex<ImperialFishEvolutionPrototype>(evolutionId, out var evolution)
                    && CheckEvolution(evolution, mate, ent, aquarium))
                    possible.Add(evolution);
            }
        }

        var types = fish.SpawnTypes?.Select(t => t.Id).ToList() ?? new List<string> { Prototype(ent)!.ID };
        string chosen;
        ImperialFishEvolutionPrototype? chosenEvolution = null;
        if (possible.Count > 0)
        {
            chosenEvolution = _random.Pick(possible);
            chosen = chosenEvolution.NewFish;
        }
        else if (second is { } partner)
        {
            var secondTypes = partner.Comp.SpawnTypes?.Select(t => t.Id).ToList() ?? new List<string> { Prototype(partner)!.ID };
            var recessive = _traits.Has(fish, "Recessive");
            var recessivePartner = _traits.Has(partner.Comp, "Recessive");
            if (fish.Overpopulated)
            {
                if (recessivePartner && !recessive)
                    return false;
                chosen = _random.Pick(secondTypes);
            }
            else if (recessive && !recessivePartner)
            {
                chosen = _random.Pick(secondTypes);
            }
            else if (recessivePartner && !recessive)
            {
                chosen = _random.Pick(types);
            }
            else
            {
                chosen = _random.Pick(secondTypes.Concat(types).ToList());
            }
        }
        else
        {
            chosen = _random.Pick(types);
        }

        CreateOffspring(ent, chosen, second, chosenEvolution);
        return true;
    }

    /// <summary>
    /// check_conditions(): особые условия эволюции, затем вне аквариума — подходящая среда,
    /// в аквариуме — шанс (×0.5 если у партнёра нет этой эволюции, ×3 от мутагена) и температура.
    /// </summary>
    public bool CheckEvolution(ImperialFishEvolutionPrototype evolution, Entity<ImperialFishComponent> source, Entity<ImperialFishComponent>? mate, EntityUid? aquarium)
    {
        var fish = source.Comp;
        switch (evolution.Condition)
        {
            case "fedLube":
                if (!FedLube(fish))
                    return false;
                break;
            case "mastodon":
                if (fish.Size < 120 || fish.Weight < 3000 || mate is { } m && (m.Comp.Size < 120 || m.Comp.Weight < 3000))
                    return false;
                break;
            case "chainsawfish":
            {
                var goldfish = AverageOf("ImperialFishGoldfish");
                if (fish.Size < goldfish.Size * 2 || fish.Weight < goldfish.Weight * 2 || !_traits.Has(fish, "Territorial"))
                    return false;
                break;
            }
            case "armoredPike":
                if (!_traits.Has(fish, "Stinger"))
                    return false;
                break;
            case "moonfish":
            {
                // Как в TG: размер сравнивается и со средним размером, и со средним весом.
                var dwarf = AverageOf("ImperialFishMoonfishDwarf");
                var moon = AverageOf("ImperialFishMoonfish");
                if (fish.Size < dwarf.Size * 1.5f && fish.Size < dwarf.Weight * 1.5f)
                    return false;
                if (mate is { } mm && mm.Comp.Size < moon.Size * 1.3f && mm.Comp.Size < moon.Weight * 1.3f)
                    return false;
                break;
            }
            case "dwarfMoonfish":
            {
                var moon = AverageOf("ImperialFishMoonfish");
                if (fish.Size > moon.Size * 0.66f && fish.Size > moon.Weight * 0.66f)
                    return false;
                if (mate is { } mm && mm.Comp.Size > moon.Size * 0.7f && mm.Comp.Size > moon.Weight * 0.7f)
                    return false;
                break;
            }
        }

        if (aquarium == null || !TryComp<ImperialAquariumComponent>(aquarium, out var aquariumComp))
            return _fish.ProperEnvironment(source);

        var probability = mate == null || mate.Value.Comp.EvolutionTypes.Contains(evolution.ID)
            ? evolution.Probability
            : evolution.Probability * 0.5f;
        if (Mutagenic(fish) || mate is { } mut && Mutagenic(mut.Comp))
            probability *= 3;

        if (!_random.Prob(Math.Clamp(probability / 100f, 0, 1)))
            return false;

        return aquariumComp.Temperature >= evolution.TemperatureMin && aquariumComp.Temperature <= evolution.TemperatureMax;
    }

    private (float Size, float Weight) AverageOf(string proto)
    {
        if (_proto.TryIndex<EntityPrototype>(proto, out var entity)
            && entity.TryGetComponent<ImperialFishComponent>(out var comp, EntityManager.ComponentFactory))
            return (comp.AverageSize, comp.AverageWeight);

        return (50, 1000);
    }

    /// <summary>create_offspring(): новая рыба рядом с родителем, наследует черты, размер и вес.</summary>
    public EntityUid CreateOffspring(Entity<ImperialFishComponent> parent, string chosenType, Entity<ImperialFishComponent>? partner, ImperialFishEvolutionPrototype? evolution)
    {
        var fish = parent.Comp;
        EntityUid uid;
        if (_container.TryGetContainingContainer(parent.Owner, out var container))
            uid = SpawnInContainerOrDrop(chosenType, container.Owner, container.ID);
        else
            uid = Spawn(chosenType, Transform(parent).Coordinates);

        if (!TryComp<ImperialFishComponent>(uid, out var newFish))
            return uid;

        Entity<ImperialFishComponent> child = (uid, newFish);
        InheritTraits(child, fish.Traits.Select(t => t.Id).ToList(), partner?.Comp.Traits.Select(t => t.Id).ToList(),
            evolution?.NewTraits.Select(t => t.Id).ToList(), evolution?.RemovedTraits.Select(t => t.Id).ToList());

        if (partner is { } mate)
        {
            var ratioSize = newFish.AverageSize * ((fish.Size / fish.AverageSize + mate.Comp.Size / mate.Comp.AverageSize) / 2);
            var meanSize = (fish.Size + mate.Comp.Size) / 2;
            // Как в TG: вес по соотношению умножается на средний размер.
            var ratioWeight = newFish.AverageSize * ((fish.Weight / fish.AverageWeight + mate.Comp.Weight / mate.Comp.AverageWeight) / 2);
            var meanWeight = (fish.Weight + mate.Comp.Weight) / 2;
            newFish.MaximumSize = 0;
            newFish.MaximumWeight = 0;
            _fish.RandomizeSizeAndWeight(child, (meanSize + ratioSize) * 0.5f, (meanWeight + ratioWeight) * 0.5f);
            mate.Comp.BreedingWait = _timing.CurTime + mate.Comp.BreedingTimeout;
        }
        else
        {
            _fish.UpdateSizeAndWeight(child, fish.Size, fish.Weight);
        }

        fish.BreedingWait = _timing.CurTime + fish.BreedingTimeout;
        newFish.BreedingWait = _timing.CurTime + newFish.BreedingTimeout * OffspringBreedingTimeoutMult;
        Dirty(child);
        return uid;
    }

    /// <summary>inherit_traits().</summary>
    public void InheritTraits(Entity<ImperialFishComponent> ent, List<string> xTraits, List<string>? yTraits, List<string>? fixedTraits, List<string>? removedTraits)
    {
        foreach (var old in ent.Comp.Traits.Select(t => t.Id).ToList())
            _traits.Remove(ent, old);

        var traits = fixedTraits?.ToList() ?? new List<string>();
        var same = yTraits == null ? new List<string>() : xTraits.Intersect(yTraits).ToList();
        var all = (yTraits == null ? xTraits : xTraits.Union(yTraits)).Except(removedTraits ?? new List<string>()).ToList();
        var incompatible = new HashSet<string>();

        var proto = Prototype(ent)?.ID ?? string.Empty;
        foreach (var (traitId, chance) in _data.GetSpontaneousTraitChances(proto))
        {
            if (!_random.Prob(chance / 100f) || !_proto.TryIndex<ImperialFishTraitPrototype>(traitId, out var trait))
                continue;
            if (traits.Any(t => trait.Incompatible.Any(i => i.Id == t)))
                continue;

            if (!traits.Contains(traitId))
                traits.Add(traitId);
            incompatible.UnionWith(trait.Incompatible.Select(i => i.Id));
        }

        foreach (var traitId in traits)
        {
            if (_proto.TryIndex<ImperialFishTraitPrototype>(traitId, out var trait))
                incompatible.UnionWith(trait.Incompatible.Select(i => i.Id));
        }

        _random.Shuffle(all);
        foreach (var traitId in all)
        {
            if (traits.Contains(traitId) || incompatible.Contains(traitId))
                continue;
            if (!_proto.TryIndex<ImperialFishTraitPrototype>(traitId, out var trait))
                continue;
            if (trait.Whitelist != null && !trait.Whitelist.Any(w => w.Id == proto))
                continue;
            if (traits.Any(t => trait.Incompatible.Any(i => i.Id == t)))
                continue;

            if (yTraits == null || same.Contains(traitId) || _random.Prob(trait.Inheritability / 100f))
            {
                traits.Add(traitId);
                incompatible.UnionWith(trait.Incompatible.Select(i => i.Id));
            }
        }

        ent.Comp.Traits = traits.Select(t => new ProtoId<ImperialFishTraitPrototype>(t)).ToList();
        _traits.ApplyAll(ent);
    }

    #endregion
}
