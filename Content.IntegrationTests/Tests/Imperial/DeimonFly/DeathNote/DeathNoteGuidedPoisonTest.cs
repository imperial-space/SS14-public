using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteGuidedPoisonTest : DeathNotePresetTestBase
{
    [Test]
    [NonParallelizable]
    public async Task FoodPoisoningContaminatesOnlyTwoRealMeals()
    {
        await SpawnTarget("FoodBurgerCheese");

        EntityUid victim = default;
        EntityUid secondMeal = default;
        EntityUid thirdMeal = default;
        EntityUid mothClothing = default;
        float firstDose = 0f;
        float secondDose = 0f;
        float thirdDose = 0f;
        float clothingDose = 0f;
        float initialVolume = 0f;
        float finalVolume = 0f;
        var poisonedCount = 0;
        var hasEdible = false;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            victim = SEntMan.SpawnEntity("MobHuman", coordinates);
            secondMeal = SEntMan.SpawnEntity("FoodBurgerCheese", coordinates);
            thirdMeal = SEntMan.SpawnEntity("FoodBurgerCheese", coordinates);
            mothClothing = SEntMan.SpawnEntity("ClothingHeadHatBeret", coordinates);

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 501;
            guide.Scenario = DeathNoteGuidedScenarioType.PoisonedFood;
            guide.ExpiresAt = STiming.CurTime + TimeSpan.FromMinutes(2);
            guide.ConsumablePoisons.Add(new DeathNoteConsumablePoisonOption
            {
                Reagent = "Ketopiride",
                MinimumQuantity = FixedPoint2.New(13),
                MaximumQuantity = FixedPoint2.New(13),
            });
            guide.PoisonedConsumableLimit = 2;

            initialVolume = GetSolutionVolume(STarget!.Value, "food");
            var ingestAttempt = new AttemptIngestEvent(victim, STarget!.Value, true);
            SEntMan.EventBus.RaiseLocalEvent(victim, ref ingestAttempt);
            RaiseInteractionAttempt(victim, mothClothing);
            RaiseInteractionAttempt(victim, secondMeal);
            RaiseInteractionAttempt(victim, thirdMeal);

            poisonedCount = guide.PoisonedConsumables.Count;
            hasEdible = SEntMan.HasComponent<Content.Shared.Nutrition.Components.EdibleComponent>(STarget.Value);
            firstDose = GetReagentQuantity(STarget.Value, "food", "Ketopiride");
            secondDose = GetReagentQuantity(secondMeal, "food", "Ketopiride");
            thirdDose = GetReagentQuantity(thirdMeal, "food", "Ketopiride");
            clothingDose = GetReagentQuantity(mothClothing, "food", "Ketopiride");
            finalVolume = GetSolutionVolume(STarget.Value, "food");
        });

        Assert.Multiple(() =>
        {
            Assert.That(hasEdible, Is.True);
            Assert.That(poisonedCount, Is.EqualTo(2));
            Assert.That(firstDose, Is.EqualTo(13f).Within(0.01f));
            Assert.That(secondDose, Is.EqualTo(13f).Within(0.01f));
            Assert.That(thirdDose, Is.Zero);
            Assert.That(clothingDose, Is.Zero);
            Assert.That(finalVolume, Is.EqualTo(initialVolume).Within(0.01f));
        });
    }

    [Test]
    public async Task ContaminatedDrinkKeepsPoisonForAnotherConsumer()
    {
        await SpawnTarget("DrinkHotCoffee");

        float initialDose = 0f;
        float remainingDose = 0f;
        float transferredDose = 0f;
        float initialVolume = 0f;
        float poisonedVolume = 0f;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var victim = SEntMan.SpawnEntity("MobHuman", coordinates);
            var otherConsumer = SEntMan.SpawnEntity("MobHuman", coordinates);

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 508;
            guide.Scenario = DeathNoteGuidedScenarioType.PoisonedDrink;
            guide.ExpiresAt = STiming.CurTime + TimeSpan.FromMinutes(2);
            guide.ConsumablePoisons.Add(new DeathNoteConsumablePoisonOption
            {
                Reagent = "Ketopiride",
                MinimumQuantity = FixedPoint2.New(13),
                MaximumQuantity = FixedPoint2.New(13),
            });

            initialVolume = GetSolutionVolume(STarget!.Value, "drink");
            RaiseInteractionAttempt(victim, STarget.Value);
            poisonedVolume = GetSolutionVolume(STarget.Value, "drink");

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Entity<SolutionComponent>? drinkSolution = null;
            Assert.That(
                solutions.ResolveSolution(
                    STarget.Value,
                    "drink",
                    ref drinkSolution,
                    out var solution),
                Is.True);

            initialDose = solution.GetTotalPrototypeQuantity("Ketopiride").Float();
            var transferred = solutions.SplitSolution(drinkSolution!.Value, FixedPoint2.New(1));
            var ingesting = new IngestingEvent(STarget.Value, transferred, false);
            SEntMan.EventBus.RaiseLocalEvent(otherConsumer, ref ingesting);

            transferredDose = ingesting.Split.GetTotalPrototypeQuantity("Ketopiride").Float();
            remainingDose = GetReagentQuantity(STarget.Value, "drink", "Ketopiride");
        });

        Assert.Multiple(() =>
        {
            Assert.That(initialDose, Is.GreaterThan(0f));
            Assert.That(poisonedVolume, Is.EqualTo(initialVolume).Within(0.01f));
            Assert.That(transferredDose, Is.GreaterThan(0f));
            Assert.That(remainingDose, Is.GreaterThan(0f));
            Assert.That(remainingDose + transferredDose, Is.EqualTo(initialDose).Within(0.01f));
        });
    }

    [Test]
    public async Task ContaminatedMealKeepsPoisonForAnotherConsumer()
    {
        await SpawnTarget("FoodBurgerCheese");

        float initialDose = 0f;
        float remainingDose = 0f;
        float ingestedDose = 0f;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var victim = SEntMan.SpawnEntity("MobHuman", coordinates);
            var otherConsumer = SEntMan.SpawnEntity("MobHuman", coordinates);

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 507;
            guide.Scenario = DeathNoteGuidedScenarioType.PoisonedFood;
            guide.ExpiresAt = STiming.CurTime + TimeSpan.FromMinutes(2);
            guide.ConsumablePoisons.Add(new DeathNoteConsumablePoisonOption
            {
                Reagent = "Ketopiride",
                MinimumQuantity = FixedPoint2.New(13),
                MaximumQuantity = FixedPoint2.New(13),
            });

            var ingestAttempt = new AttemptIngestEvent(victim, STarget!.Value, true);
            SEntMan.EventBus.RaiseLocalEvent(victim, ref ingestAttempt);

            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            Entity<SolutionComponent>? foodSolution = null;
            Assert.That(
                solutions.ResolveSolution(
                    STarget.Value,
                    "food",
                    ref foodSolution,
                    out var solution),
                Is.True);

            initialDose = solution.GetTotalPrototypeQuantity("Ketopiride").Float();
            var split = solutions.SplitSolution(foodSolution!.Value, FixedPoint2.New(1));
            var ingesting = new IngestingEvent(STarget.Value, split, false);
            SEntMan.EventBus.RaiseLocalEvent(otherConsumer, ref ingesting);

            ingestedDose = ingesting.Split.GetTotalPrototypeQuantity("Ketopiride").Float();
            remainingDose = GetReagentQuantity(STarget.Value, "food", "Ketopiride");
        });

        Assert.Multiple(() =>
        {
            Assert.That(initialDose, Is.GreaterThan(0f));
            Assert.That(ingestedDose, Is.GreaterThan(0f),
                "A different consumer must ingest the physical poison stored in the meal.");
            Assert.That(remainingDose, Is.GreaterThan(0f),
                "Poison must remain in the uneaten part of the meal.");
            Assert.That(remainingDose + ingestedDose, Is.EqualTo(initialDose).Within(0.01f));
        });
    }

    [Test]
    [NonParallelizable]
    public async Task DrinkPoisoningContaminatesOnlyTwoNonEmptyBeverages()
    {
        await SpawnTarget("DrinkHotCoffee");

        EntityUid victim = default;
        EntityUid secondDrink = default;
        EntityUid thirdDrink = default;
        EntityUid emptyCup = default;
        EntityUid meal = default;
        float firstDose = 0f;
        float secondDose = 0f;
        float thirdDose = 0f;
        float mealDose = 0f;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            victim = SEntMan.SpawnEntity("MobHuman", coordinates);
            secondDrink = SEntMan.SpawnEntity("DrinkColaCan", coordinates);
            thirdDrink = SEntMan.SpawnEntity("DrinkHotCoco", coordinates);
            emptyCup = SEntMan.SpawnEntity("DrinkWaterCup", coordinates);
            meal = SEntMan.SpawnEntity("FoodBurgerCheese", coordinates);

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 502;
            guide.Scenario = DeathNoteGuidedScenarioType.PoisonedDrink;
            guide.ExpiresAt = STiming.CurTime + TimeSpan.FromMinutes(2);
            guide.ConsumablePoisons.Add(new DeathNoteConsumablePoisonOption
            {
                Reagent = "Ketopiride",
                MinimumQuantity = FixedPoint2.New(13),
                MaximumQuantity = FixedPoint2.New(13),
            });
            guide.PoisonedConsumableLimit = 2;

            RaiseInteractionAttempt(victim, meal);
            RaiseInteractionAttempt(victim, emptyCup);
            RaiseInteractionAttempt(victim, STarget!.Value);
            RaiseInteractionAttempt(victim, secondDrink);
            RaiseInteractionAttempt(victim, thirdDrink);

            firstDose = GetReagentQuantity(STarget.Value, "drink", "Ketopiride");
            secondDose = GetReagentQuantity(secondDrink, "drink", "Ketopiride");
            thirdDose = GetReagentQuantity(thirdDrink, "drink", "Ketopiride");
            mealDose = GetReagentQuantity(meal, "food", "Ketopiride");
        });

        Assert.Multiple(() =>
        {
            Assert.That(firstDose, Is.EqualTo(13f).Within(0.01f));
            Assert.That(secondDose, Is.EqualTo(13f).Within(0.01f));
            Assert.That(thirdDose, Is.Zero);
            Assert.That(mealDose, Is.Zero);
        });
    }

    [Test]
    public async Task DrinkPoisoningDoesNotContaminateNonItemDrinkSources()
    {
        await SpawnTarget("DrinkHotCoffee");

        var poisonedCount = -1;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var victim = SEntMan.SpawnEntity("MobHuman", coordinates);
            var puddle = SEntMan.SpawnEntity("Puddle", coordinates);
            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 509;
            guide.Scenario = DeathNoteGuidedScenarioType.PoisonedDrink;
            guide.ExpiresAt = STiming.CurTime + TimeSpan.FromMinutes(2);
            guide.ConsumablePoisons.Add(new DeathNoteConsumablePoisonOption
            {
                Reagent = "Ketopiride",
                MinimumQuantity = FixedPoint2.New(13),
                MaximumQuantity = FixedPoint2.New(13),
            });

            RaiseInteractionAttempt(victim, puddle);
            poisonedCount = guide.PoisonedConsumables.Count;
        });

        Assert.That(poisonedCount, Is.Zero);
    }

    [Test]
    public async Task PoisonPolicyStartsOnlyWhenTheVictimActuallyIngestsThePoison()
    {
        await SpawnTarget("FoodBurgerCheese");

        EntityUid victim = default;
        const uint entryId = 503;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            PrepareGuidedEntry(entryId, victim);

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = entryId;
            guide.Scenario = DeathNoteGuidedScenarioType.PoisonedFood;
            guide.ExpiresAt = STiming.CurTime + TimeSpan.FromMinutes(2);
            guide.ConsumablePoisons.Add(new DeathNoteConsumablePoisonOption
            {
                Reagent = "Ketopiride",
                MinimumQuantity = FixedPoint2.New(1),
                MaximumQuantity = FixedPoint2.New(1),
            });

            Assert.That(
                SEntMan.HasComponent<Content.Shared.Traits.Assorted.UnrevivableComponent>(victim),
                Is.False);

            var split = new Solution("Ketopiride", FixedPoint2.New(1));
            var ingesting = new IngestingEvent(STarget!.Value, split, false);
            SEntMan.EventBus.RaiseLocalEvent(victim, ref ingesting);
        });

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(journal.TryGetEntry(entryId, out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<DeathNoteGuidedScenarioComponent>(victim).Triggered, Is.True);
            Assert.That(
                SEntMan.HasComponent<Content.Shared.Traits.Assorted.UnrevivableComponent>(victim),
                Is.True);
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.True);
            Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.EffectStarted));
        });
    }

    private void RaiseInteractionAttempt(EntityUid user, EntityUid target)
    {
        var attempt = new InteractionAttemptEvent(user, target);
        SEntMan.EventBus.RaiseLocalEvent(user, ref attempt);
    }

    private float GetReagentQuantity(EntityUid entity, string solutionName, string reagent)
    {
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        Entity<SolutionComponent>? solutionEntity = null;
        Assert.That(
            solutions.ResolveSolution(entity, solutionName, ref solutionEntity, out var solution),
            Is.True);
        return solution.GetTotalPrototypeQuantity(reagent).Float();
    }

    private float GetSolutionVolume(EntityUid entity, string solutionName)
    {
        var solutions = SEntMan.System<SharedSolutionContainerSystem>();
        Entity<SolutionComponent>? solutionEntity = null;
        Assert.That(
            solutions.ResolveSolution(entity, solutionName, ref solutionEntity, out var solution),
            Is.True);
        return solution.Volume.Float();
    }

}
