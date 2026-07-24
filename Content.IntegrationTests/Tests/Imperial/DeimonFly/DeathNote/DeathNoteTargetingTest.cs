using System.Collections.Generic;
using System.Numerics;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.Server.ImmovableRod;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.NPC;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteTargetingTest : DeathNotePresetTestBase
{
    private static readonly ProtoId<DeathNotePresetPrototype> RodPreset = "DeathNoteImmovableRod";
    private static readonly ProtoId<DeathNotePresetPrototype> CarpPreset = "DeathNoteCarp";
    private static readonly ProtoId<DeathNotePresetPrototype> MimicPreset = "DeathNoteMimic";

    [Test]
    public async Task ImmovableRodPresetUsesConfiguredRodPrototype()
    {
        await SpawnTarget("DeathNote");

        DeathNotePresetExecutionResult result = default;
        EntityUid? spawnedRod = null;
        await Server.WaitPost(() =>
        {
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(RodPreset);
            Assert.That(registry.TryGetHandler(DeathNotePresetHandlerType.ImmovableRod, out var handler), Is.True);
            Assert.That(handler, Is.Not.Null);

            result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, SPlayer),
                preset.Parameters);

            var query = SEntMan.AllEntityQueryEnumerator<ImmovableRodComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == "DeathNoteImmovableRod")
                {
                    spawnedRod = uid;
                    break;
                }
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(spawnedRod, Is.Not.Null);
            Assert.That(SEntMan.HasComponent<DeathNoteHomingRodComponent>(spawnedRod!.Value), Is.True);
        });
    }

    [Test]
    public async Task ImmovableRodGuaranteesTargetImpactThenContinuesStraight()
    {
        await SpawnTarget("DeathNote");

        EntityUid victim = default;
        EntityUid rod = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates.Offset(new Vector2(2f, 0f)));
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(RodPreset);
            Assert.That(registry.TryGetHandler(DeathNotePresetHandlerType.ImmovableRod, out var handler), Is.True);
            var result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, victim),
                preset.Parameters);
            Assert.That(result.Success, Is.True, result.Error);

            var query = SEntMan.AllEntityQueryEnumerator<DeathNoteHomingRodComponent>();
            Assert.That(query.MoveNext(out rod, out _), Is.True);
        });

        await RunTicks(180);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.Deleted(victim), Is.True, "The target body should receive the standard rod result.");
            Assert.That(SEntMan.Deleted(rod), Is.False, "The rod must remain after its target dies.");
            Assert.That(SEntMan.HasComponent<DeathNoteHomingRodComponent>(rod), Is.False,
                "Homing must detach so the rod continues on its last straight course.");
        });
    }

    [Test]
    public async Task CarpPresetSpawnsRiftAndExactlyThreeAssignedCarps()
    {
        await SpawnTarget("DeathNote");

        EntityUid victim = default;
        Vector2 executionPosition = default;
        DeathNotePresetExecutionResult result = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates.Offset(new Vector2(2f, 0f)));
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(
                victim,
                SEntMan.GetComponent<TransformComponent>(victim).Coordinates.Offset(new Vector2(5f, 0f)));
            executionPosition = SEntMan.GetComponent<TransformComponent>(victim).LocalPosition;
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(CarpPreset);
            Assert.That(registry.TryGetHandler(DeathNotePresetHandlerType.Fauna, out var handler), Is.True);
            result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, victim),
                preset.Parameters);
        });

        var riftCount = 0;
        var assignedCarps = 0;
        EntityUid rift = default;
        await Server.WaitPost(() =>
        {
            var entities = SEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (entities.MoveNext(out var uid, out var metadata))
            {
                if (metadata.EntityPrototype?.ID == "DeathNoteCarpRift")
                {
                    riftCount++;
                    rift = uid;
                }
            }

            var targets = SEntMan.AllEntityQueryEnumerator<DeathNotePriorityTargetComponent>();
            while (targets.MoveNext(out _, out var priority))
            {
                if (priority.Target == victim)
                    assignedCarps++;
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(riftCount, Is.EqualTo(1));
            Assert.That(assignedCarps, Is.EqualTo(3));
            Assert.That(Vector2.Distance(
                    SEntMan.GetComponent<TransformComponent>(rift).LocalPosition,
                    executionPosition),
                Is.LessThan(0.01f), "The rift must use the target's position at execution time.");
        });

        await Server.WaitPost(() =>
        {
            var npc = SEntMan.System<NPCSystem>();
            var targets = SEntMan.AllEntityQueryEnumerator<DeathNotePriorityTargetComponent, HTNComponent>();
            while (targets.MoveNext(out var uid, out var priority, out _))
            {
                if (priority.Target == victim)
                    npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, SPlayer);
            }
        });
        await RunTicks(25);

        await Server.WaitPost(() =>
        {
            var targets = SEntMan.AllEntityQueryEnumerator<DeathNotePriorityTargetComponent, HTNComponent>();
            while (targets.MoveNext(out _, out var priority, out var htn))
            {
                if (priority.Target != victim)
                    continue;

                Assert.That(
                    htn.Blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var ordered, SEntMan),
                    Is.True);
                Assert.That(ordered, Is.EqualTo(victim));
            }
        });
    }

    [Test]
    public async Task MimicWakesWithTheAssignedPriorityTarget()
    {
        await SpawnTarget("VendingMachineCola");

        DeathNotePresetExecutionResult result = default;
        EntityUid? spawnedMimic = null;
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(STarget!.Value).Coordinates.Offset(new Vector2(8f, 0f)));
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(MimicPreset);
            Assert.That(registry.TryGetHandler(DeathNotePresetHandlerType.Mimic, out var handler), Is.True);

            result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, victim),
                preset.Parameters);

            var query = SEntMan.AllEntityQueryEnumerator<DeathNoteMimicComponent, HTNComponent>();
            while (query.MoveNext(out var uid, out var mimic, out var htn))
            {
                if (mimic.Target != victim)
                    continue;

                Assert.That(
                    htn.Blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var ordered, SEntMan),
                    Is.True);
                Assert.That(ordered, Is.EqualTo(victim));
                Assert.That(mimic.TargetHitsRemaining, Is.InRange(2, 3));
                Assert.That(mimic.TargetDamageRemaining, Is.InRange(200f, 395f));
                spawnedMimic = uid;
                break;
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(spawnedMimic, Is.Not.Null);
            Assert.That(SEntMan.HasComponent<ActiveNPCComponent>(spawnedMimic!.Value), Is.True);
        });

        await Server.WaitPost(() =>
        {
            var npc = SEntMan.System<NPCSystem>();
            npc.SetBlackboard(spawnedMimic!.Value, NPCBlackboard.CurrentOrderedTarget, SPlayer);
        });
        await RunTicks(25);
        await Server.WaitPost(() =>
        {
            var htn = SEntMan.GetComponent<HTNComponent>(spawnedMimic!.Value);
            Assert.That(
                htn.Blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var ordered, SEntMan),
                Is.True);
            Assert.That(ordered, Is.EqualTo(victim),
                "The mimic must continuously restore its assigned victim as the ordered target.");
        });

        var configuredHits = SEntMan.GetComponent<DeathNoteMimicComponent>(spawnedMimic!.Value).TargetHitsRemaining;
        await Server.WaitPost(() =>
        {
            var melee = SEntMan.GetComponent<MeleeWeaponComponent>(spawnedMimic.Value);
            var firstHit = new MeleeHitEvent(
                new List<EntityUid> { victim },
                spawnedMimic.Value,
                spawnedMimic.Value,
                melee.Damage,
                null);
            SEntMan.EventBus.RaiseLocalEvent(spawnedMimic.Value, firstHit);
        });

        var damage = SEntMan.System<Content.Shared.Damage.Systems.DamageableSystem>();
        var damageable = SEntMan.GetComponent<DamageableComponent>(victim);
        Assert.That(damage.GetPositiveDamage((victim, damageable)).GetTotal().Float(), Is.LessThan(200f),
            "The first mimic strike must not contain the full lethal damage budget.");

        for (var hit = 1; hit < configuredHits; hit++)
        {
            await Server.WaitPost(() =>
            {
                var melee = SEntMan.GetComponent<MeleeWeaponComponent>(spawnedMimic.Value);
                var nextHit = new MeleeHitEvent(
                    new List<EntityUid> { victim },
                    spawnedMimic.Value,
                    spawnedMimic.Value,
                    melee.Damage,
                    null);
                SEntMan.EventBus.RaiseLocalEvent(spawnedMimic.Value, nextHit);
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<DeathNoteMimicComponent>(spawnedMimic.Value).TargetHitsRemaining,
                Is.Zero);
            Assert.That(
                damage.GetPositiveDamage((victim, damageable)).GetTotal().Float(),
                Is.InRange(200f, 395f));
        });
    }

}
