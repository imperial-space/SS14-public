using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNoteJournalTest : InteractionTest
{
    [Test]
    public async Task RejectedEntriesAreBoundWithoutEvictingNotebookEntries()
    {
        await Server.WaitAssertion(() =>
        {
            var journal = SEntMan.System<DeathNoteJournalSystem>();
            journal.ClearRound();

            try
            {
                var acceptedId = journal.AllocateEntryId();
                Assert.That(
                    journal.AddEntry(CreateEntry(acceptedId, SPlayer, 0, 0, DeathNoteEntryStatus.DeathConfirmed)),
                    Is.True);

                var rejectedLimit = journal.RejectedEntryLimit;
                Assert.That(rejectedLimit, Is.GreaterThan(0));

                uint oldestRejectedId = 0;
                uint newestRejectedId = 0;
                for (var i = 0; i <= rejectedLimit; i++)
                {
                    newestRejectedId = journal.AllocateEntryId();
                    if (i == 0)
                        oldestRejectedId = newestRejectedId;

                    Assert.That(
                        journal.AddEntry(CreateEntry(
                            newestRejectedId,
                            SPlayer,
                            -1,
                            -1,
                            DeathNoteEntryStatus.InvalidFormat)),
                        Is.True);
                }

                Assert.Multiple(() =>
                {
                    Assert.That(journal.EntryCount, Is.EqualTo(rejectedLimit + 1));
                    Assert.That(journal.TryGetEntry(acceptedId, out _), Is.True);
                    Assert.That(journal.TryGetEntry(oldestRejectedId, out _), Is.False);
                    Assert.That(journal.TryGetEntry(newestRejectedId, out _), Is.True);
                    Assert.That(journal.GetReversePage(-1, 0).Single().EntryId, Is.EqualTo(newestRejectedId));
                });

                const int pageSize = 7;
                var pageCount = (journal.EntryCount + pageSize - 1) / pageSize;
                var oldestPage = journal.GetReversePage(pageCount - 1, pageSize);
                Assert.That(oldestPage.Select(entry => entry.EntryId), Does.Contain(acceptedId));
            }
            finally
            {
                journal.ClearRound();
            }
        });
    }

    private static DeathNoteEntry CreateEntry(
        uint entryId,
        EntityUid entity,
        int pageIndex,
        int lineIndex,
        DeathNoteEntryStatus status)
    {
        return new DeathNoteEntry(
            entryId,
            entity,
            entity,
            entity,
            pageIndex,
            lineIndex,
            null,
            "notebook",
            "owner",
            "writer",
            "original",
            "target",
            string.Empty,
            "cause",
            null,
            DeathNoteEntryType.Standard,
            null,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            status);
    }
}
