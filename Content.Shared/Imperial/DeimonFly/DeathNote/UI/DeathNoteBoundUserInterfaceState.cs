using System.Collections.Immutable;
using Content.Shared.Imperial.DeimonFly.DeathNote.Models;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.UI;

/// <summary>
/// Полностью безопасное состояние обычного интерфейса.
/// Истинная цель, внутренний статус и ошибки поиска сюда не входят.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteBoundUserInterfaceState : BoundUserInterfaceState
{
    public LocId RulePage { get; }
    public ImmutableArray<DeathNotePublicEntry> Entries { get; }
    public int SpreadIndex { get; }
    public int MaxEntryTextLength { get; }
    public int WritablePageCount { get; }
    public int EntriesPerPage { get; }
    public uint SubmissionRevision { get; }
    public bool CanSubmit { get; }

    public DeathNoteBoundUserInterfaceState(
        LocId rulePage,
        ImmutableArray<DeathNotePublicEntry> entries,
        int spreadIndex,
        int maxEntryTextLength,
        int writablePageCount,
        int entriesPerPage,
        uint submissionRevision,
        bool canSubmit)
    {
        RulePage = rulePage;
        Entries = entries;
        SpreadIndex = spreadIndex;
        MaxEntryTextLength = maxEntryTextLength;
        WritablePageCount = writablePageCount;
        EntriesPerPage = entriesPerPage;
        SubmissionRevision = submissionRevision;
        CanSubmit = canSubmit;
    }
}
