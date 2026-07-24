using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteOwnershipVisibilityTest : InteractionTest
{
    [Test]
    public async Task FirstPickupAssignsPersistentOwnerAndForeignHolderIsTemporary()
    {
        await SpawnTarget("DeathNote");
        var notebook = STarget!.Value;

        await Pickup();

        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var owner = SEntMan.GetComponent<DeathNoteOwnerComponent>(SPlayer);
        Assert.Multiple(() =>
        {
            Assert.That(runtime.OwnerEntity, Is.EqualTo(SPlayer));
            Assert.That(runtime.CurrentHolder, Is.Null);
            Assert.That(owner.Notebooks, Does.Contain(notebook));
        });

        await Drop();
        Assert.That(runtime.OwnerEntity, Is.EqualTo(SPlayer));

        EntityUid foreignHolder = default;
        HandsComponent foreignHands = null!;
        await Server.WaitPost(() =>
        {
            foreignHolder = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            foreignHands = SEntMan.GetComponent<HandsComponent>(foreignHolder);
            Assert.That(HandSys.TryPickupAnyHand(
                foreignHolder,
                notebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: foreignHands));
        });
        await RunTicks(1);

        var holder = SEntMan.GetComponent<DeathNoteHolderComponent>(foreignHolder);
        Assert.Multiple(() =>
        {
            Assert.That(runtime.OwnerEntity, Is.EqualTo(SPlayer));
            Assert.That(runtime.CurrentHolder, Is.EqualTo(foreignHolder));
            Assert.That(holder.Notebooks, Does.Contain(notebook));
        });

        await Server.WaitPost(() =>
        {
            Assert.That(HandSys.TryDrop(
                (foreignHolder, foreignHands),
                notebook,
                checkActionBlocker: false,
                doDropInteraction: false));
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(runtime.OwnerEntity, Is.EqualTo(SPlayer));
            Assert.That(runtime.CurrentHolder, Is.Null);
            Assert.That(SEntMan.HasComponent<DeathNoteHolderComponent>(foreignHolder), Is.False);
        });
    }

    [Test]
    public async Task MultipleNotebookRelationsSurviveIndividualRemoval()
    {
        await SpawnTarget("DeathNote");
        var firstNotebook = STarget!.Value;

        await Pickup();
        await Drop();

        EntityUid secondNotebook = default;
        EntityUid foreignHolder = default;
        HandsComponent foreignHands = null!;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            secondNotebook = SEntMan.SpawnEntity("DeathNote", coordinates);
            Assert.That(HandSys.TryPickupAnyHand(
                SPlayer,
                secondNotebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: Hands));
            Assert.That(HandSys.TryDrop(
                (SPlayer, Hands),
                secondNotebook,
                checkActionBlocker: false,
                doDropInteraction: false));

            foreignHolder = SEntMan.SpawnEntity("MobHuman", coordinates);
            foreignHands = SEntMan.GetComponent<HandsComponent>(foreignHolder);
            Assert.That(HandSys.TryPickupAnyHand(
                foreignHolder,
                firstNotebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: foreignHands));
            Assert.That(HandSys.TryPickupAnyHand(
                foreignHolder,
                secondNotebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: foreignHands));
        });
        await RunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<DeathNoteOwnerComponent>(SPlayer).Notebooks,
                Is.EquivalentTo(new[] { firstNotebook, secondNotebook }));
            Assert.That(SEntMan.GetComponent<DeathNoteHolderComponent>(foreignHolder).Notebooks,
                Is.EquivalentTo(new[] { firstNotebook, secondNotebook }));
        });

        await Server.WaitPost(() =>
        {
            Assert.That(HandSys.TryDrop(
                (foreignHolder, foreignHands),
                firstNotebook,
                checkActionBlocker: false,
                doDropInteraction: false));
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<DeathNoteHolderComponent>(foreignHolder), Is.True);
            Assert.That(SEntMan.GetComponent<DeathNoteHolderComponent>(foreignHolder).Notebooks,
                Is.EquivalentTo(new[] { secondNotebook }));
            Assert.That(SEntMan.GetComponent<DeathNoteRuntimeComponent>(firstNotebook).CurrentHolder, Is.Null);
            Assert.That(SEntMan.GetComponent<DeathNoteRuntimeComponent>(secondNotebook).CurrentHolder,
                Is.EqualTo(foreignHolder));
        });
    }

    [Test]
    public async Task RemovingOwnerComponentReleasesNotebookForNextPickup()
    {
        await SpawnTarget("DeathNote");
        var notebook = STarget!.Value;

        await Pickup();
        await Drop();

        await Server.WaitPost(() => SEntMan.RemoveComponent<DeathNoteOwnerComponent>(SPlayer));
        await RunTicks(1);

        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        Assert.That(runtime.OwnerEntity, Is.Null);

        EntityUid nextOwner = default;
        await Server.WaitPost(() =>
        {
            nextOwner = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var nextOwnerHands = SEntMan.GetComponent<HandsComponent>(nextOwner);
            Assert.That(HandSys.TryPickupAnyHand(
                nextOwner,
                notebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: nextOwnerHands));
        });
        await RunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(runtime.OwnerEntity, Is.EqualTo(nextOwner));
            Assert.That(SEntMan.GetComponent<DeathNoteOwnerComponent>(nextOwner).Notebooks,
                Does.Contain(notebook));
        });
    }

    [Test]
    public async Task ShinigamiVisibilityIsLimitedToOwnerAndCurrentHolder()
    {
        await SpawnTarget("DeathNote");
        var notebook = STarget!.Value;

        await Pickup();
        await Drop();

        EntityUid ordinaryPlayer = default;
        EntityUid shinigami = default;
        HandsComponent ordinaryHands = null!;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            ordinaryPlayer = SEntMan.SpawnEntity("MobHuman", coordinates);
            ordinaryHands = SEntMan.GetComponent<HandsComponent>(ordinaryPlayer);
            shinigami = SEntMan.SpawnEntity("MobHuman", coordinates);
            SEntMan.EnsureComponent<DeathNoteShinigamiComponent>(shinigami);
        });
        await RunTicks(2);

        var shinigamiLayer = (int) DeathNoteVisibilityLayers.Shinigami;
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<EyeComponent>(SPlayer).VisibilityMask & shinigamiLayer,
                Is.EqualTo(shinigamiLayer));
            Assert.That(SEntMan.GetComponent<EyeComponent>(ordinaryPlayer).VisibilityMask & shinigamiLayer,
                Is.Zero);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(shinigami).VisibilityMask & shinigamiLayer,
                Is.EqualTo(shinigamiLayer));
            Assert.That(SEntMan.HasComponent<GhostComponent>(shinigami), Is.False);
        });

        await Server.WaitPost(() =>
        {
            Assert.That(HandSys.TryPickupAnyHand(
                ordinaryPlayer,
                notebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: ordinaryHands));
        });
        await RunTicks(1);

        Assert.That(SEntMan.GetComponent<EyeComponent>(ordinaryPlayer).VisibilityMask & shinigamiLayer,
            Is.EqualTo(shinigamiLayer));

        await Server.WaitPost(() =>
        {
            Assert.That(HandSys.TryDrop(
                (ordinaryPlayer, ordinaryHands),
                notebook,
                checkActionBlocker: false,
                doDropInteraction: false));
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<EyeComponent>(ordinaryPlayer).VisibilityMask & shinigamiLayer,
                Is.Zero);
            Assert.That(SEntMan.GetComponent<EyeComponent>(SPlayer).VisibilityMask & shinigamiLayer,
                Is.EqualTo(shinigamiLayer));
        });

        await Server.WaitPost(() => SEntMan.RemoveComponent<DeathNoteShinigamiComponent>(shinigami));
        await RunTicks(1);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(shinigami).VisibilityMask & shinigamiLayer,
                Is.Zero);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(shinigami).VisibilityMask &
                        (int) VisibilityFlags.Normal,
                Is.EqualTo((int) VisibilityFlags.Normal));
        });
    }

    [Test]
    public async Task ThreeOwnershipChannelsKeepShinigamiVisibilityIsolated()
    {
        await SpawnTarget("DeathNote");

        EntityUid owner2 = default;
        EntityUid owner3 = default;
        EntityUid note2 = default;
        EntityUid note3 = default;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            owner2 = SEntMan.SpawnEntity("MobHuman", coordinates);
            owner3 = SEntMan.SpawnEntity("MobHuman", coordinates);
            note2 = SEntMan.SpawnEntity("DeathNote2", coordinates);
            note3 = SEntMan.SpawnEntity("DeathNote3", coordinates);

            Assert.That(HandSys.TryPickupAnyHand(SPlayer, STarget!.Value, false, false, false, Hands));
            Assert.That(HandSys.TryPickupAnyHand(
                owner2, note2, false, false, false, SEntMan.GetComponent<HandsComponent>(owner2)));
            Assert.That(HandSys.TryPickupAnyHand(
                owner3, note3, false, false, false, SEntMan.GetComponent<HandsComponent>(owner3)));

            var god1 = SEntMan.SpawnEntity("MobHuman", coordinates);
            var god2 = SEntMan.SpawnEntity("MobHuman", coordinates);
            var god3 = SEntMan.SpawnEntity("MobHuman", coordinates);
            SEntMan.EnsureComponent<DeathNoteShinigamiComponent>(god1);
            SEntMan.EnsureComponent<DeathNoteShinigami2Component>(god2);
            SEntMan.EnsureComponent<DeathNoteShinigami3Component>(god3);
        });
        await RunTicks(2);

        var allLayers = (int) (DeathNoteVisibilityLayers.Shinigami |
                               DeathNoteVisibilityLayers.Shinigami2 |
                               DeathNoteVisibilityLayers.Shinigami3);
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.GetComponent<EyeComponent>(SPlayer).VisibilityMask & allLayers,
                Is.EqualTo(DeathNoteVisibilityLayers.Shinigami));
            Assert.That(SEntMan.GetComponent<EyeComponent>(owner2).VisibilityMask & allLayers,
                Is.EqualTo(DeathNoteVisibilityLayers.Shinigami2));
            Assert.That(SEntMan.GetComponent<EyeComponent>(owner3).VisibilityMask & allLayers,
                Is.EqualTo(DeathNoteVisibilityLayers.Shinigami3));
            Assert.That(SEntMan.GetComponent<DeathNoteOwner2Component>(owner2).Notebooks,
                Does.Contain(note2));
            Assert.That(SEntMan.GetComponent<DeathNoteOwner3Component>(owner3).Notebooks,
                Does.Contain(note3));
        });
    }

    [TestCase("DeathGodNote")]
    [TestCase("DeathGodNoteUnrevivable")]
    public async Task ShinigamiNotebookGrantsAllTemporaryChannelsWithoutAnOwner(string prototype)
    {
        await SpawnTarget(prototype);
        var notebook = STarget!.Value;

        await Pickup();
        await RunTicks(2);

        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var allLayers = (int) (DeathNoteVisibilityLayers.Shinigami |
                               DeathNoteVisibilityLayers.Shinigami2 |
                               DeathNoteVisibilityLayers.Shinigami3);
        Assert.Multiple(() =>
        {
            Assert.That(runtime.OwnerEntity, Is.Null);
            Assert.That(runtime.CurrentHolder, Is.EqualTo(SPlayer));
            Assert.That(SEntMan.HasComponent<DeathNoteOwnerComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteOwner2Component>(SPlayer), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteOwner3Component>(SPlayer), Is.False);
            Assert.That(SEntMan.GetComponent<DeathNoteHolderComponent>(SPlayer).Notebooks,
                Does.Contain(notebook));
            Assert.That(SEntMan.GetComponent<DeathNoteHolder2Component>(SPlayer).Notebooks,
                Does.Contain(notebook));
            Assert.That(SEntMan.GetComponent<DeathNoteHolder3Component>(SPlayer).Notebooks,
                Does.Contain(notebook));
            Assert.That(SEntMan.GetComponent<EyeComponent>(SPlayer).VisibilityMask & allLayers,
                Is.EqualTo(allLayers));
        });

        await Drop();
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(runtime.OwnerEntity, Is.Null);
            Assert.That(runtime.CurrentHolder, Is.Null);
            Assert.That(SEntMan.HasComponent<DeathNoteHolderComponent>(SPlayer), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteHolder2Component>(SPlayer), Is.False);
            Assert.That(SEntMan.HasComponent<DeathNoteHolder3Component>(SPlayer), Is.False);
            Assert.That(SEntMan.GetComponent<EyeComponent>(SPlayer).VisibilityMask & allLayers,
                Is.Zero);
        });
    }

    [Test]
    public async Task DeletedNotebookAndOwnerCleanMultipleRelations()
    {
        await SpawnTarget("DeathNote");
        var firstNotebook = STarget!.Value;

        EntityUid secondNotebook = default;
        EntityUid ownerEntity = default;
        HandsComponent ownerHands = null!;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            secondNotebook = SEntMan.SpawnEntity("DeathNote", coordinates);
            ownerEntity = SEntMan.SpawnEntity("MobHuman", coordinates);
            ownerHands = SEntMan.GetComponent<HandsComponent>(ownerEntity);

            Assert.That(HandSys.TryPickupAnyHand(
                ownerEntity,
                firstNotebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: ownerHands));
            Assert.That(HandSys.TryPickupAnyHand(
                ownerEntity,
                secondNotebook,
                checkActionBlocker: false,
                animateUser: false,
                animate: false,
                handsComp: ownerHands));
            Assert.That(HandSys.TryDrop(
                (ownerEntity, ownerHands),
                firstNotebook,
                checkActionBlocker: false,
                doDropInteraction: false));
            Assert.That(HandSys.TryDrop(
                (ownerEntity, ownerHands),
                secondNotebook,
                checkActionBlocker: false,
                doDropInteraction: false));
        });
        await RunTicks(1);

        await Server.WaitPost(() => SEntMan.QueueDeleteEntity(firstNotebook));
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<DeathNoteOwnerComponent>(ownerEntity), Is.True);
            Assert.That(SEntMan.GetComponent<DeathNoteOwnerComponent>(ownerEntity).Notebooks,
                Is.EquivalentTo(new[] { secondNotebook }));
            Assert.That(SEntMan.GetComponent<DeathNoteRuntimeComponent>(secondNotebook).OwnerEntity,
                Is.EqualTo(ownerEntity));
        });

        await Server.WaitPost(() => SEntMan.QueueDeleteEntity(ownerEntity));
        await RunTicks(2);

        Assert.That(SEntMan.GetComponent<DeathNoteRuntimeComponent>(secondNotebook).OwnerEntity, Is.Null);
    }

}
