using System.Linq;
using System.Numerics;
using Content.Server.Doors;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.Server.Wires;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Interaction.Events;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteGuidedWorldTest : DeathNotePresetTestBase
{
    private static readonly ProtoId<DeathNotePresetPrototype> BluespacePreset = "DeathNoteBluespaceAnomaly";
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    [TestPrototypes]
    private const string DisposalVictimPrototype = """
        - type: entity
          id: DeathNoteDisposalTestVictim
          components:
          - type: Damageable
            damageContainer: Biological
        """;

    [Test]
    public async Task AirlockScenarioArmsOnlyTheFirstInteractedDoor()
    {
        await SpawnTarget("AirlockMaint");

        EntityUid secondAirlock = default;
        EntityUid victim = default;
        TimeSpan originalCloseTimeOne = default;
        TimeSpan originalCloseTimeTwo = default;
        bool originalCanCrush = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            secondAirlock = SEntMan.SpawnEntity(
                "AirlockMaint",
                SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates.Offset(new Vector2(2f, 0f)));

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 42;
            guide.Scenario = DeathNoteGuidedScenarioType.AirlockAccident;
            guide.ExpiresAt = TimeSpan.Zero;
            guide.PersistentUntilTriggered = true;
            guide.MinimumDamage = 200f;
            guide.MaximumDamage = 395f;
            guide.Damage = CreateBluntDamage(1f);
            guide.AirlockForceCloseDelay = TimeSpan.FromSeconds(0.1);
            guide.DoorCloseStageDuration = TimeSpan.FromSeconds(0.05);
            guide.AirlockAutoCloseDelayModifier = 0.04f;
            guide.DoorwayImpactRadius = 1.1f;

            PrepareGuidedEntry(guide.EntryId, victim);
        });

        await RunTicks(5);
        Assert.That(SEntMan.HasComponent<DeathNoteGuidedScenarioComponent>(victim), Is.True,
            "A persistent airlock fate must not expire while the target has not opened an airlock.");

        await Server.WaitPost(() =>
        {
            var doors = SEntMan.System<SharedDoorSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var firstDoor = SEntMan.GetComponent<DoorComponent>(STarget!.Value);
            var secondDoor = SEntMan.GetComponent<DoorComponent>(secondAirlock);
            originalCloseTimeOne = firstDoor.CloseTimeOne;
            originalCloseTimeTwo = firstDoor.CloseTimeTwo;
            originalCanCrush = firstDoor.CanCrush;
            var powered = new PowerChangedEvent(true, 0f);
            SEntMan.EventBus.RaiseLocalEvent(STarget.Value, ref powered);
            SEntMan.EventBus.RaiseLocalEvent(secondAirlock, ref powered);
            transform.SetCoordinates(victim, SEntMan.GetComponent<TransformComponent>(STarget.Value).Coordinates);

            Assert.That(doors.TryOpen(STarget.Value, firstDoor, victim), Is.True,
                "The test victim must cause the same BeforeDoorOpened event used by click and bump opening.");
            Assert.That(doors.TryOpen(secondAirlock, secondDoor, victim), Is.True);
        });

        var firstDeadly = SEntMan.GetComponent<DeathNoteDeadlyAirlockComponent>(STarget!.Value);
        var firstAirlock = SEntMan.GetComponent<AirlockComponent>(STarget.Value);
        var firstDoor = SEntMan.GetComponent<DoorComponent>(STarget.Value);
        var safetyWire = SEntMan.System<WiresSystem>()
            .TryGetWires<DoorSafetyWireAction>(STarget.Value)
            .Single();
        Assert.Multiple(() =>
        {
            Assert.That(firstDeadly.Target, Is.EqualTo(victim));
            Assert.That(firstDeadly.Damage.GetTotal().Float(), Is.InRange(200f, 395f));
            Assert.That(firstAirlock.Safety, Is.False);
            Assert.That(safetyWire.IsCut, Is.True,
                "The scenario must physically cut the safety wire until a player repairs it.");
            Assert.That(firstDoor.CloseTimeOne.TotalSeconds, Is.EqualTo(0.05).Within(0.001));
            Assert.That(firstDoor.CloseTimeTwo.TotalSeconds, Is.EqualTo(0.05).Within(0.001));
            Assert.That(SEntMan.HasComponent<DeathNoteDeadlyAirlockComponent>(secondAirlock), Is.False);
        });

        await RunTicks(80);

        Assert.Multiple(() =>
        {
            Assert.That(firstDeadly.LethalImpactApplied, Is.True,
                $"The armed airlock must force its own close and apply damage at the unsafe partial-close phase. " +
                $"state={firstDoor.State}, forceCloseAt={firstDeadly.ForceCloseAt}, safety={firstAirlock.Safety}, " +
                $"componentPresent={SEntMan.HasComponent<DeathNoteDeadlyAirlockComponent>(STarget.Value)}, " +
                $"doorPos={SEntMan.GetComponent<TransformComponent>(STarget.Value).LocalPosition}, " +
                $"targetPos={SEntMan.GetComponent<TransformComponent>(victim).LocalPosition}");
            Assert.That(firstDoor.CanCrush, Is.False,
                "The Death Note impact must not keep crushing the dead body on later door cycles.");
            Assert.That(safetyWire.IsCut, Is.True,
                "The safety wire must remain cut after the impact until somebody repairs it.");
            Assert.That(firstAirlock.Safety, Is.False);
        });

        await Server.WaitPost(() =>
        {
            Assert.That(safetyWire.Action, Is.Not.Null);
            Assert.That(safetyWire.Action!.Mend(EntityUid.Invalid, safetyWire), Is.True);
            safetyWire.IsCut = false;
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(firstAirlock.Safety, Is.True);
            Assert.That(safetyWire.IsCut, Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteDeadlyAirlockComponent>(STarget.Value), Is.False,
                "Repairing the safety wire must clear the event-specific airlock state.");
            Assert.That(firstDoor.CloseTimeOne, Is.EqualTo(originalCloseTimeOne));
            Assert.That(firstDoor.CloseTimeTwo, Is.EqualTo(originalCloseTimeTwo));
            Assert.That(firstDoor.CanCrush, Is.EqualTo(originalCanCrush));
        });
    }

    [Test]
    public async Task VendingScenarioTopplesMachineAndKillsAssignedVictim()
    {
        await SpawnTarget("VendingMachineCola");

        EntityUid victim = default;
        Vector2 impactPosition = default;
        await Server.WaitPost(() =>
        {
            var vendingCoordinates = SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates;
            victim = SEntMan.SpawnEntity("MobHuman", vendingCoordinates.Offset(new Vector2(1f, 0f)));
            impactPosition = SEntMan.GetComponent<TransformComponent>(victim).LocalPosition;

            var guide = SEntMan.EnsureComponent<DeathNoteGuidedScenarioComponent>(victim);
            guide.EntryId = 77;
            guide.Scenario = DeathNoteGuidedScenarioType.VendingMachineCrush;
            guide.ExpiresAt = TimeSpan.FromHours(1);
            guide.MinimumDamage = 200f;
            guide.MaximumDamage = 395f;
            guide.Damage = CreateBluntDamage(1f);
            guide.VendingFallDuration = TimeSpan.FromSeconds(0.55);

            PrepareGuidedEntry(guide.EntryId, victim);
            var attempt = new InteractionAttemptEvent(victim, STarget);
            SEntMan.EventBus.RaiseLocalEvent(victim, ref attempt);
        });

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<DeathNoteFallingVendingComponent>(STarget!.Value), Is.True);
            Assert.That(SEntMan.HasComponent<DeathNoteFallenVendingVisualComponent>(STarget.Value), Is.True);
            Assert.That(SEntMan.HasComponent<DeathNoteVendingVictimComponent>(victim), Is.True);
        });

        await RunTicks(45);

        var mobState = SEntMan.System<MobStateSystem>();
        var fallenTransform = SEntMan.GetComponent<TransformComponent>(STarget!.Value);
        Assert.Multiple(() =>
        {
            Assert.That(mobState.IsDead(victim), Is.True);
            Assert.That(SEntMan.HasComponent<DeathNoteFallingVendingComponent>(STarget.Value), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteVendingVictimComponent>(victim), Is.False);
            Assert.That(Vector2.Distance(fallenTransform.LocalPosition, impactPosition), Is.LessThan(0.01f));
        });
    }

    [Test]
    public async Task DisposalImpactsFinishAndCleanUpWhenTheUnitDisappears()
    {
        await SpawnTarget("DisposalUnit");

        EntityUid victim = default;
        float initialBluntDamage = 0f;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "DeathNoteDisposalTestVictim",
                MapCoordinates.Nullspace);
            var damageable = SEntMan.GetComponent<DamageableComponent>(victim);
            var initialDamage = SEntMan.System<Content.Shared.Damage.Systems.DamageableSystem>()
                .GetPositiveDamage((victim, damageable));
            initialBluntDamage = initialDamage.DamageDict.TryGetValue("Blunt", out var initialBlunt)
                ? initialBlunt.Float()
                : 0f;
            var effect = SEntMan.EnsureComponent<DeathNoteDisposalVictimComponent>(victim);
            effect.DisposalUnit = STarget!.Value;
            effect.NextImpact = STiming.CurTime + TimeSpan.FromSeconds(0.05);
            effect.ImpactInterval = TimeSpan.FromSeconds(0.05);
            effect.ImpactsRemaining = 3;
            effect.DamagePerImpact = CreateBluntDamage(80f);
            effect.ExpiresAt = STiming.CurTime + TimeSpan.FromSeconds(1);

            SEntMan.DeleteEntity(STarget.Value);
        });

        await RunTicks(30);

        var damage = SEntMan.System<Content.Shared.Damage.Systems.DamageableSystem>();
        var damageable = SEntMan.GetComponent<DamageableComponent>(victim);
        var bluntDamage = damage.GetPositiveDamage((victim, damageable)).DamageDict["Blunt"];
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<DeathNoteDisposalVictimComponent>(victim), Is.False,
                "The temporary disposal effect must remove itself after its last impact.");
            Assert.That(bluntDamage.Float() - initialBluntDamage, Is.EqualTo(240f).Within(0.01f),
                "Deleting the disposal unit must not stall the remaining impacts.");
        });
    }

    [Test]
    public async Task BluespacePocketIsDeletedWhenTheVictimDisappears()
    {
        await SpawnTarget("DeathNote");

        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(BluespacePreset);
            Assert.That(registry.TryGetHandler(DeathNotePresetHandlerType.BluespaceAnomaly, out var handler), Is.True);
            var result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, victim),
                preset.Parameters);
            Assert.That(result.Success, Is.True, result.Error);
        });

        await RunTicks(40);

        EntityUid pocketMap = default;
        await Server.WaitPost(() =>
        {
            var effect = SEntMan.GetComponent<DeathNoteBluespaceVictimComponent>(victim);
            Assert.That(effect.Teleported, Is.True);
            Assert.That(effect.PocketMap, Is.Not.Null);
            pocketMap = effect.PocketMap!.Value;
            SEntMan.DeleteEntity(victim);
        });
        await RunTicks(5);

        Assert.That(SEntMan.Deleted(pocketMap), Is.True,
            "Deleting the victim must also delete the private bluespace pocket map.");
    }

    private DamageSpecifier CreateBluntDamage(float amount)
    {
        return new DamageSpecifier(
            Server.ProtoMan.Index(BluntDamage),
            FixedPoint2.New(amount));
    }
}
