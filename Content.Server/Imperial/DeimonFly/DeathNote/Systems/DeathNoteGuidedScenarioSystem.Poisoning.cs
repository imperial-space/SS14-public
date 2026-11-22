using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Item;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteGuidedScenarioSystem
{
    private void OnFoodIngested(
        Entity<DeathNoteGuidedScenarioComponent> ent,
        ref IngestingEvent args)
    {
        var guide = ent.Comp;
        if (guide.Scenario is not (DeathNoteGuidedScenarioType.PoisonedFood or
            DeathNoteGuidedScenarioType.PoisonedDrink) ||
            _timing.CurTime >= guide.ExpiresAt ||
            guide.Triggered ||
            !ContainsScenarioPoison(args.Split, guide))
        {
            return;
        }

        // Реагент должен поступить из настоящего раствора еды или напитка. Проверка поглощённой
        // порции также поддерживает отравленные напитки, перелитые в другую ёмкость.
        if (!TryStartGuidedEffect(ent))
            return;

        guide.Triggered = true;
    }

    private void OnItemPickedUp(
        Entity<DeathNoteGuidedScenarioComponent> ent,
        ref DidEquipHandEvent args)
    {
        TryPoisonTouchedConsumable(ent, args.Equipped);
    }

    private void OnAttemptIngest(
        Entity<DeathNoteGuidedScenarioComponent> ent,
        ref AttemptIngestEvent args)
    {
        if (args.Ingest)
            TryPoisonTouchedConsumable(ent, args.Ingested);
    }

    private bool TryPoisonTouchedConsumable(
        Entity<DeathNoteGuidedScenarioComponent> ent,
        EntityUid target)
    {
        var guide = ent.Comp;
        if (_timing.CurTime >= guide.ExpiresAt ||
            guide.PoisonedConsumables.Count >= guide.PoisonedConsumableLimit ||
            guide.PoisonedConsumables.Contains(target) ||
            !TryComp(target, out EdibleComponent? edible))
        {
            return false;
        }

        var validConsumable = guide.Scenario switch
        {
            // FoodBase добавляет SpaceGarbage настоящей еде, в отличие от одежды
            // и прочих необычных съедобных для молей материалов.
            DeathNoteGuidedScenarioType.PoisonedFood =>
                edible.Edible == IngestionSystem.Food &&
                HasComp<SpaceGarbageComponent>(target),
            DeathNoteGuidedScenarioType.PoisonedDrink =>
                edible.Edible == IngestionSystem.Drink &&
                HasComp<ItemComponent>(target),
            _ => false,
        };
        if (!validConsumable)
            return false;

        Entity<SolutionComponent>? solutionEntity = null;
        if (!_solutionContainer.ResolveSolution(
                target,
                edible.Solution,
                ref solutionEntity,
                out var solution) ||
            solution.Volume <= FixedPoint2.Zero ||
            !TrySelectConsumablePoison(guide, solution.Volume, out var reagent, out var quantity))
        {
            return false;
        }

        // Яд физически заменяет равный объём содержимого: ни объём порции,
        // ни максимальная вместимость предмета не изменяются.
        _solutionContainer.SplitSolution(solutionEntity.Value, quantity);

        if (!_solutionContainer.TryAddReagent(
            solutionEntity.Value,
            reagent,
            quantity,
            out var accepted) ||
            accepted < quantity)
        {
            return false;
        }

        guide.PoisonedConsumables.Add(target);
        return true;
    }

    private bool TrySelectConsumablePoison(
        DeathNoteGuidedScenarioComponent guide,
        FixedPoint2 replaceableVolume,
        out ProtoId<ReagentPrototype> reagent,
        out FixedPoint2 quantity)
    {
        foreach (var option in guide.ConsumablePoisons)
        {
            if (option.MinimumQuantity <= FixedPoint2.Zero ||
                option.MaximumQuantity < option.MinimumQuantity ||
                replaceableVolume < option.MinimumQuantity ||
                !_prototypes.HasIndex<ReagentPrototype>(option.Reagent))
            {
                continue;
            }

            reagent = option.Reagent;
            quantity = RandomFixed(
                option.MinimumQuantity,
                FixedPoint2.Min(option.MaximumQuantity, replaceableVolume));
            return true;
        }

        reagent = default;
        quantity = FixedPoint2.Zero;
        return false;
    }

    private FixedPoint2 RandomFixed(FixedPoint2 minimum, FixedPoint2 maximum)
    {
        if (maximum <= minimum)
            return minimum;

        return FixedPoint2.New(_random.NextFloat(minimum.Float(), maximum.Float()));
    }

    private static bool ContainsScenarioPoison(
        Solution solution,
        DeathNoteGuidedScenarioComponent guide)
    {
        foreach (var option in guide.ConsumablePoisons)
        {
            if (solution.GetTotalPrototypeQuantity(option.Reagent) > FixedPoint2.Zero)
                return true;
        }

        return false;
    }
}
