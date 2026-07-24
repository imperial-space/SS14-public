using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Server.Imperial.DeimonFly.DeathNote.Systems;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public abstract class DeathNotePresetTestBase : InteractionTest
{
    protected void PrepareGuidedEntry(uint entryId, EntityUid target)
    {
        var ticker = SEntMan.System<GameTicker>();
        if (ticker.RunLevel != GameRunLevel.InRound)
        {
            Server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
            ticker.StartRound(true);
        }

        var notebook = STarget ?? target;
        var roundTime = ticker.RoundDuration();
        var entry = new DeathNoteEntry(
            entryId,
            notebook,
            null,
            SPlayer,
            0,
            0,
            target,
            "notebook",
            string.Empty,
            "writer",
            "original",
            "target",
            "target",
            "guided scenario",
            null,
            DeathNoteEntryType.Preset,
            null,
            roundTime,
            roundTime + TimeSpan.FromMinutes(2),
            true,
            DeathNoteEntryStatus.Scheduled);

        Assert.That(SEntMan.System<DeathNoteJournalSystem>().AddEntry(entry), Is.True);
    }
}
