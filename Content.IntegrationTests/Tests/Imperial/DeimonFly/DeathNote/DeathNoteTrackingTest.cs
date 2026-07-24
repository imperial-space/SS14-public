using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteTrackingTest : DeathNotePresetTestBase
{
    [Test]
    public async Task ExistingUnrevivablePolicyIsPreservedDuringAndAfterTracking()
    {
        await SpawnTarget("FoodBurgerCheese");

        EntityUid victim = default;
        DeathNoteGuidedEffectStartEvent effectStart = default;
        const uint entryId = 504;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);

            var existingPolicy = SEntMan.EnsureComponent<UnrevivableComponent>(victim);
            existingPolicy.Analyzable = true;
            existingPolicy.Cloneable = true;
            existingPolicy.ReasonMessage = "defibrillator-unrevivable";

            effectStart = StartTrackedGuidedEffect(entryId, victim, STiming.TickPeriod * 2);
        });

        Assert.That(effectStart.Prepared, Is.True);
        var tracker = SEntMan.GetComponent<DeathNoteTargetTrackingComponent>(victim);
        Assert.That(tracker.OwnsUnrevivableComponent, Is.False);

        await RunTicks(4);

        var preservedPolicy = SEntMan.GetComponent<UnrevivableComponent>(victim);
        Assert.Multiple(() =>
        {
            Assert.That(preservedPolicy.Analyzable, Is.True);
            Assert.That(preservedPolicy.Cloneable, Is.True);
            Assert.That(preservedPolicy.ReasonMessage, Is.EqualTo("defibrillator-unrevivable"));
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.False);
        });
    }

    [Test]
    public async Task DeathNoteUnrevivableIsRemovedWhenTrackingExpiresWhileTargetLives()
    {
        await SpawnTarget("FoodBurgerCheese");

        EntityUid victim = default;
        DeathNoteGuidedEffectStartEvent effectStart = default;
        const uint entryId = 505;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            effectStart = StartTrackedGuidedEffect(entryId, victim, STiming.TickPeriod * 2);
        });

        Assert.Multiple(() =>
        {
            Assert.That(effectStart.Prepared, Is.True);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(victim), Is.True);
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.True);
        });

        await RunTicks(4);

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(journal.TryGetEntry(entryId, out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(victim), Is.False);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(victim), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.False);
            Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.EffectStarted));
        });
    }

    [Test]
    public async Task DeathAfterTrackingExpiryDoesNotConfirmEntryOrKeepUnrevivable()
    {
        await SpawnTarget("FoodBurgerCheese");

        EntityUid victim = default;
        const uint entryId = 506;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var effectStart = StartTrackedGuidedEffect(entryId, victim, STiming.TickPeriod * 2);
            Assert.That(effectStart.Prepared, Is.True);
        });

        await RunTicks(4);
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(victim), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.False);
        });

        await Server.WaitPost(() =>
        {
            SEntMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
        });
        await RunTicks(2);

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(journal.TryGetEntry(entryId, out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.EffectStarted));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(victim), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.False);
        });
    }

    [Test]
    public async Task RoundRuleKeepsDeathNoteOwnedUnrevivableDuringTheSameDeathEvent()
    {
        await SpawnTarget("DeathNote");

        EntityUid victim = default;
        DeathNoteGuidedEffectStartEvent effectStart = default;
        var ruleStarted = false;
        const uint entryId = 507;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            effectStart = StartTrackedGuidedEffect(entryId, victim, TimeSpan.FromMinutes(1));

            var ticker = SEntMan.System<GameTicker>();
            ruleStarted = ticker.StartGameRule("DeathNoteRoundUnrevivable");
            Assert.That(SEntMan.System<DeathNoteRoundUnrevivableRuleSystem>().IsActive(), Is.True);

            SEntMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Dead);
        });
        await RunTicks(2);

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(journal.TryGetEntry(entryId, out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(effectStart.Prepared, Is.True);
            Assert.That(ruleStarted, Is.True);
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(victim), Is.True);
            Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.DeathConfirmed));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(victim), Is.True);
            Assert.That(SEntMan.HasComponent<DeathNoteTargetTrackingComponent>(victim), Is.False);
        });
    }

    [Test]
    public async Task RoundRuleMarksFormerPlayerBodiesThatWereAlreadyDead()
    {
        await SpawnTarget("DeathNote");

        EntityUid formerPlayerBody = default;
        var ruleStarted = false;
        await Server.WaitPost(() =>
        {
            formerPlayerBody = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession, formerPlayerBody);
            Assert.That(
                SEntMan.HasComponent<DeathNotePlayerBodyComponent>(formerPlayerBody),
                Is.True);

            SEntMan.System<MobStateSystem>().ChangeMobState(formerPlayerBody, MobState.Dead);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            Assert.That(
                SEntMan.HasComponent<UnrevivableComponent>(formerPlayerBody),
                Is.False,
                "The marker alone must not prevent revival before the rule starts.");

            ruleStarted = SEntMan.System<GameTicker>().StartGameRule("DeathNoteRoundUnrevivable");
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(ruleStarted, Is.True);
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(formerPlayerBody), Is.True);
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(formerPlayerBody), Is.True);
        });
    }

    [Test]
    public async Task GibbedTrackedTargetConfirmsEntryBeforeDeletion()
    {
        await SpawnTarget("DeathNote");

        EntityUid victim = default;
        const uint entryId = 508;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var effectStart = StartTrackedGuidedEffect(entryId, victim, TimeSpan.FromMinutes(1));
            Assert.That(effectStart.Prepared, Is.True);

            SEntMan.System<GibbingSystem>().Gib(victim);
        });
        await RunTicks(2);

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(journal.TryGetEntry(entryId, out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.DeathConfirmed));
            Assert.That(SEntMan.Deleted(victim), Is.True);
        });
    }

    private DeathNoteGuidedEffectStartEvent StartTrackedGuidedEffect(
        uint entryId,
        EntityUid target,
        TimeSpan trackingDuration)
    {
        PrepareGuidedEntry(entryId, target);

        var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(target);
        guide.EntryId = entryId;
        guide.Scenario = DeathNoteGuidedScenarioType.PoisonedFood;
        guide.EffectTrackingDuration = trackingDuration;

        var effectStart = new DeathNoteGuidedEffectStartEvent(entryId);
        SEntMan.EventBus.RaiseLocalEvent(target, ref effectStart);
        return effectStart;
    }

}
