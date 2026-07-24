using System.Collections.Generic;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteSubmissionTest : InteractionTest
{
    [Test]
    public async Task ReaderWithoutPenCannotSubmit()
    {
        await SpawnTarget("DeathNote");
        await Activate();

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
        });
        await RunTicks(5);

        var scheduled = ticker.RoundDuration() + TimeSpan.FromSeconds(10);
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                $"Definitely Missing at {FormatRoundTime(scheduled)} shall die from a heart attack.",
                runtime.SubmissionRevision));

        Assert.That(runtime.EntryIds, Is.Empty);
    }

    [Test]
    public async Task WritingToolCanSubmitToNotebookOutsideHands()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        Assert.That(IsUiOpen(DeathNoteUiKey.Notebook), Is.True);

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var notebookComponent = SEntMan.GetComponent<DeathNoteComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        await Server.WaitPost(() =>
        {
            notebookComponent.SubmissionCooldown = TimeSpan.Zero;
            notebookComponent.EntriesPerPage = 2;
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
        });
        await RunTicks(5);

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
        Assert.That(SUiSys.IsUiOpen(notebook, DeathNoteUiKey.Notebook, SPlayer), Is.True);
        var originalRevision = runtime.SubmissionRevision;
        // Третья строка страницы 2 и несуществующая страница 31 должны быть отклонены.
        foreach (var pageIndex in new[] { 0, 1, 1, 1, 30, 2, 29 })
        {
            await SendBui(
                DeathNoteUiKey.Notebook,
                new DeathNoteSubmitMessage(
                    pageIndex,
                    "Definitely Missing at 00:10:00 shall die from a heart attack.",
                    runtime.SubmissionRevision));
        }

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        var storedLocations = new List<(int PageIndex, int LineIndex)>();
        foreach (var entryId in runtime.EntryIds)
        {
            Assert.That(journal.TryGetEntry(entryId, out var entry), Is.True);
            Assert.That(entry, Is.Not.Null);
            Assert.That(
                entry!.OriginalText,
                Is.EqualTo("Definitely Missing at 00:10:00 shall die from a heart attack."));
            storedLocations.Add((entry.PageIndex, entry.LineIndex));
        }

        Assert.Multiple(() =>
        {
            Assert.That(runtime.EntryIds, Has.Count.EqualTo(5));
            Assert.That(runtime.SubmissionRevision, Is.Not.EqualTo(originalRevision));
            Assert.That(storedLocations, Is.EqualTo(new[]
            {
                (0, 0),
                (1, 0),
                (1, 1),
                (2, 0),
                (29, 0),
            }));
        });
    }

    [Test]
    public async Task CustomScenarioWithPastTimeWithoutConnectedTargetFailsImmediately()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.System<MetaDataSystem>().SetEntityName(victim, "Scheduled Victim");
        });
        await RunTicks(5);

        var scheduled = TimeSpan.Zero;
        var writtenText =
            $"Scheduled Victim at {FormatRoundTime(scheduled)} shall fulfill: leave the bridge.";
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                writtenText,
                runtime.SubmissionRevision));

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(runtime.EntryIds, Has.Count.EqualTo(1));
        Assert.That(journal.TryGetEntry(runtime.EntryIds[0], out var entry), Is.True);
        Assert.That(entry!.OriginalText, Is.EqualTo(writtenText));
        Assert.That(entry.ScheduledRoundTime, Is.EqualTo(entry.CreatedRoundTime));
        Assert.That(entry.Status, Is.EqualTo(DeathNoteEntryStatus.Failed),
            "The influence must fail immediately when the resolved target has no connected session.");
    }

    [Test]
    public async Task GuidedPresetRejectsExplicitTimeInsideMinimumExecutionDelay()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
        });
        await RunTicks(5);

        var scheduled = ticker.RoundDuration() + TimeSpan.FromSeconds(30);
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                $"Too Soon Victim at {FormatRoundTime(scheduled)} shall die from disposal catastrophe.",
                runtime.SubmissionRevision));

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        var rejected = journal.GetReversePage(0, 1);
        Assert.Multiple(() =>
        {
            Assert.That(runtime.EntryIds, Is.Empty);
            Assert.That(rejected, Has.Count.EqualTo(1));
            Assert.That(rejected[0].FailureReason, Is.EqualTo(DeathNoteFailureReason.TimeTooSoon));
            Assert.That(rejected[0].TargetEntity, Is.Null);
        });
    }

    [Test]
    public async Task GuidedPresetWithoutTimeUsesMinimumExecutionDelay()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.System<MetaDataSystem>().SetEntityName(victim, "Guided Default Victim");
        });
        await RunTicks(5);

        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                "Guided Default Victim shall die from disposal catastrophe.",
                runtime.SubmissionRevision));

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(runtime.EntryIds, Has.Count.EqualTo(1));
        Assert.That(journal.TryGetEntry(runtime.EntryIds[0], out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entry!.TargetEntity, Is.EqualTo(victim));
            Assert.That(entry.Status, Is.EqualTo(DeathNoteEntryStatus.Scheduled));
            Assert.That(
                entry.ScheduledRoundTime - entry.CreatedRoundTime,
                Is.EqualTo(TimeSpan.FromMinutes(2)));
        });
    }

    [Test]
    public async Task AirlockPresetAcceptsExecutionInsideFormerTwoMinuteLimit()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.System<MetaDataSystem>().SetEntityName(victim, "Airlock Timing Victim");
        });
        await RunTicks(5);

        var scheduled = ticker.RoundDuration() + TimeSpan.FromSeconds(30);
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                $"Airlock Timing Victim at {FormatRoundTime(scheduled)} shall die from airlock accident.",
                runtime.SubmissionRevision));

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(runtime.EntryIds, Has.Count.EqualTo(1));
        Assert.That(journal.TryGetEntry(runtime.EntryIds[0], out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entry!.TargetEntity, Is.EqualTo(victim));
            Assert.That(entry.Status, Is.EqualTo(DeathNoteEntryStatus.Scheduled));
            Assert.That(entry.ScheduledRoundTime - entry.CreatedRoundTime, Is.LessThan(TimeSpan.FromMinutes(2)));
        });
    }

    [Test]
    public async Task TargetResolutionTreatsYoAndYeAsEquivalent()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.System<MetaDataSystem>().SetEntityName(victim, "Семён Елкин");
        });
        await RunTicks(5);

        const string writtenText = "Семен Елкин shall die from a heart attack.";
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(0, writtenText, runtime.SubmissionRevision));

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(runtime.EntryIds, Has.Count.EqualTo(1));
        Assert.That(journal.TryGetEntry(runtime.EntryIds[0], out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entry!.TargetEntity, Is.EqualTo(victim));
            Assert.That(entry.Status, Is.EqualTo(DeathNoteEntryStatus.Scheduled));
            Assert.That(entry.OriginalText, Is.EqualTo(writtenText));
        });
    }

    [Test]
    public async Task UnrevivablePolicyStartsWithAutomaticEffect()
    {
        await SpawnTarget("DeathNoteUnrevivable");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.System<MetaDataSystem>().SetEntityName(victim, "Irreversible Victim");
        });
        await RunTicks(5);

        var scheduled = ticker.RoundDuration() + TimeSpan.FromSeconds(14);
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                $"Irreversible Victim at {FormatRoundTime(scheduled)} shall die from a heart attack.",
                runtime.SubmissionRevision));

        Assert.That(SEntMan.HasComponent<Content.Shared.Traits.Assorted.UnrevivableComponent>(victim), Is.False);
        await RunTicks((int) Math.Ceiling(15d / TickPeriod));
        Assert.That(SEntMan.HasComponent<Content.Shared.Traits.Assorted.UnrevivableComponent>(victim), Is.True);

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(journal.TryGetEntry(runtime.EntryIds[0], out var entry), Is.True);
        Assert.That(entry!.Status, Is.EqualTo(DeathNoteEntryStatus.DeathConfirmed));
    }

    [Test]
    public async Task CriticalTargetCanStillBeResolvedAndScheduled()
    {
        await SpawnTarget("DeathNote");
        await InteractUsing("Pen");

        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var ticker = SEntMan.System<GameTicker>();
        EntityUid victim = default;
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
            Server.PlayerMan.SetAttachedEntity(ServerSession, SPlayer);
            victim = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            SEntMan.System<MetaDataSystem>().SetEntityName(victim, "Critical Victim");
            SEntMan.System<MobStateSystem>().ChangeMobState(victim, MobState.Critical);
        });
        await RunTicks(5);

        var scheduled = ticker.RoundDuration() + TimeSpan.FromSeconds(20);
        await SendBui(
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmitMessage(
                0,
                $"Critical Victim at {FormatRoundTime(scheduled)} shall die from a heart attack.",
                runtime.SubmissionRevision));

        var journal = SEntMan.System<DeathNoteJournalSystem>();
        Assert.That(runtime.EntryIds, Has.Count.EqualTo(1));
        Assert.That(journal.TryGetEntry(runtime.EntryIds[0], out var entry), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(entry!.TargetEntity, Is.EqualTo(victim));
            Assert.That(entry.Status, Is.EqualTo(DeathNoteEntryStatus.Scheduled));
        });
    }

    private static string FormatRoundTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }
}
