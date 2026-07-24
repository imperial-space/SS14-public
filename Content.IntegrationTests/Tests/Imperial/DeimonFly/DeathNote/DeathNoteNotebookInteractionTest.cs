using System.Linq;
using System.Numerics;
using Content.Client.Imperial.DeimonFly.DeathNote.UI;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteNotebookInteractionTest : InteractionTest
{
    [Test]
    public async Task NotebookSpreadStateIsBoundedAndServerClampsForgedRequests()
    {
        await SpawnTarget("DeathNote");
        var notebook = STarget!.Value;
        var runtime = SEntMan.GetComponent<DeathNoteRuntimeComponent>(notebook);
        var component = SEntMan.GetComponent<DeathNoteComponent>(notebook);

        await Server.WaitPost(() =>
        {
            component.WritablePageCount = 30;
            component.EntriesPerPage = 12;
            PopulateNotebook(runtime, notebook, component.WritablePageCount, component.EntriesPerPage);
        });

        await Activate();

        var state = await GetNotebookState();
        Assert.Multiple(() =>
        {
            Assert.That(state.SpreadIndex, Is.Zero);
            Assert.That(state.Entries, Has.Length.EqualTo(12));
            Assert.That(state.Entries.All(entry => entry.PageIndex == 0), Is.True);
        });

        await SendBui(DeathNoteUiKey.Notebook, new DeathNoteSpreadRequestMessage(1));
        state = await GetNotebookState();
        Assert.Multiple(() =>
        {
            Assert.That(state.SpreadIndex, Is.EqualTo(1));
            Assert.That(state.Entries, Has.Length.EqualTo(24));
            Assert.That(state.Entries.All(entry => entry.PageIndex is 1 or 2), Is.True);
        });

        await SendBui(DeathNoteUiKey.Notebook, new DeathNoteSpreadRequestMessage(int.MaxValue));
        state = await GetNotebookState();
        Assert.Multiple(() =>
        {
            Assert.That(state.SpreadIndex, Is.EqualTo(15));
            Assert.That(state.Entries, Has.Length.EqualTo(12));
            Assert.That(state.Entries.All(entry => entry.PageIndex == 29), Is.True);
        });

        await SendBui(DeathNoteUiKey.Notebook, new DeathNoteSpreadRequestMessage(int.MinValue));
        state = await GetNotebookState();
        Assert.Multiple(() =>
        {
            Assert.That(state.SpreadIndex, Is.Zero);
            Assert.That(state.Entries, Has.Length.LessThanOrEqualTo(2 * component.EntriesPerPage));
            Assert.That(state.Entries.All(entry => entry.PageIndex == 0), Is.True);
        });
    }

    [Test]
    public async Task SpreadRequestAfterLosingAccessClosesNotebook()
    {
        await SpawnTarget("DeathNote");
        await Activate();

        await Server.WaitPost(() =>
        {
            var transform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Transform.SetWorldPosition((SPlayer, transform), new Vector2(20f, 20f));
        });

        await SendBui(DeathNoteUiKey.Notebook, new DeathNoteSpreadRequestMessage(1));

        Assert.That(IsUiOpen(DeathNoteUiKey.Notebook), Is.False);
    }

    [Test]
    public async Task AnyoneCanOpenNotebookWithoutWritingTool()
    {
        await SpawnTarget("DeathNote");

        Assert.That(IsUiOpen(DeathNoteUiKey.Notebook), Is.False);

        await Activate();

        Assert.That(IsUiOpen(DeathNoteUiKey.Notebook), Is.True);
    }

    [Test]
    public async Task NonWritingToolDoesNotInterceptNotebookInteraction()
    {
        await SpawnTarget("DeathNote");

        await InteractUsing("Crowbar");

        Assert.That(IsUiOpen(DeathNoteUiKey.Notebook), Is.False);
    }

    private void PopulateNotebook(
        DeathNoteRuntimeComponent runtime,
        EntityUid notebook,
        int writablePageCount,
        int entriesPerPage)
    {
        var journal = SEntMan.System<DeathNoteJournalSystem>();
        for (var pageIndex = 0; pageIndex < writablePageCount; pageIndex++)
        {
            for (var lineIndex = 0; lineIndex < entriesPerPage; lineIndex++)
            {
                var entryId = journal.AllocateEntryId();
                var originalText = $"Page {pageIndex}, line {lineIndex}";
                var entry = new DeathNoteEntry(
                    entryId,
                    notebook,
                    null,
                    SPlayer,
                    pageIndex,
                    lineIndex,
                    null,
                    "Death Note",
                    string.Empty,
                    "Writer",
                    originalText,
                    "Target",
                    string.Empty,
                    "heart attack",
                    null,
                    DeathNoteEntryType.Standard,
                    null,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    false,
                    DeathNoteEntryStatus.Submitted);

                Assert.That(journal.AddEntry(entry), Is.True);
                runtime.EntryIds.Add(entryId);
            }
        }
    }

    private async Task<DeathNoteBoundUserInterfaceState> GetNotebookState()
    {
        DeathNoteBoundUserInterfaceState state = default!;
        await Client.WaitPost(() =>
        {
            Assert.That(TryGetBui(DeathNoteUiKey.Notebook, out var bui), Is.True);
            Assert.That(bui, Is.InstanceOf<DeathNoteBoundUserInterface>());
            state = ((DeathNoteBoundUserInterface) bui!).LastState;
        });

        Assert.That(state, Is.Not.Null);
        return state;
    }
}
