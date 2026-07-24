using Content.Client.Imperial.DeimonFly.DeathNote.Systems;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CCVar;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteCustomInfluenceTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task EverySupportedBangFormImmediatelyReachesTheConnectedVictim()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var notebookComponent = SEntMan.GetComponent<DeathNoteComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        await Server.WaitPost(() =>
        {
            notebookComponent.SubmissionCooldown = TimeSpan.Zero;
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            SEntMan.System<MetaDataSystem>().SetEntityName(SPlayer, "Олег Гребенюк");
        });
        await RunTicks(5);

        var influence = CEntMan.System<DeathNoteInfluenceSystem>();
        var initialInfluenceCount = influence.ReceivedInfluenceCount;
        var journal = SEntMan.System<DeathNoteJournalSystem>();
        var pastTimeInput =
            $"!{SEntMan.GetComponent<MetaDataComponent>(SPlayer).EntityName} at 00:00:00 shall wait.";
        var cases = new[]
        {
            (pastTimeInput, pastTimeInput[1..]),
            (
                "Олег Гребенюк !в 00:21:40 застрелит себя, оставив записку «я иду за вами».",
                "Олег Гребенюк в 00:21:40 застрелит себя, оставив записку «я иду за вами»."),
            (
                "Олег Гребенюк !застрелит себя, оставив записку «я иду за вами».",
                "Олег Гребенюк застрелит себя, оставив записку «я иду за вами»."),
            (
                "Олег Гребенюк в 00:21:40 !застрелит себя, оставив записку «я иду за вами».",
                "Олег Гребенюк в 00:21:40 застрелит себя, оставив записку «я иду за вами»."),
            (
                "!Олег Гребенюк в 00:21:40 застрелит себя, оставив записку «я иду за вами».",
                "Олег Гребенюк в 00:21:40 застрелит себя, оставив записку «я иду за вами»."),
            (
                "Олег Гребенюк !будубебебебе",
                "Олег Гребенюк будубебебебе"),
        };

        for (var index = 0; index < cases.Length; index++)
        {
            var (input, expectedText) = cases[index];
            await SendBui(
                DeathNoteUiKey.Notebook,
                new DeathNoteSubmitMessage(0, input, runtime.SubmissionRevision));
            await RunTicks(5);

            Assert.That(runtime.EntryIds, Has.Count.EqualTo(index + 1), input);
            Assert.That(journal.TryGetEntry(runtime.EntryIds[index], out var entry), Is.True, input);
            Assert.Multiple(() =>
            {
                Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.CustomDelivered), input);
                Assert.That(entry.OriginalText, Is.EqualTo(expectedText), input);
                Assert.That(entry.CustomText, Is.EqualTo(expectedText), input);
                Assert.That(entry.ScheduledRoundTime, Is.EqualTo(entry.CreatedRoundTime), input);
                Assert.That(influence.ReceivedInfluenceCount,
                    Is.EqualTo(initialInfluenceCount + index + 1),
                    input);
                Assert.That(influence.LastScenario, Is.EqualTo(expectedText), input);
            });
        }
    }
}
