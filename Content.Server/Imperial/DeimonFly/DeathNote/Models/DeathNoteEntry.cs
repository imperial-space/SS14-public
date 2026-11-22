using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Models;

/// <summary>
/// Полная серверная запись текущего раунда.
/// Строки-снимки сохраняют административную ценность после удаления сущностей.
/// </summary>
public sealed class DeathNoteEntry
{
    public uint EntryId { get; }
    public EntityUid NotebookEntity { get; }
    public EntityUid? OwnerEntity { get; }
    public EntityUid WriterEntity { get; }
    public int PageIndex { get; }
    public int LineIndex { get; }
    public EntityUid? TargetEntity { get; }
    public string NotebookSnapshot { get; }
    public string OwnerSnapshot { get; }
    public string WriterSnapshot { get; }
    public string OriginalText { get; }
    public string EnteredTargetName { get; }
    public string ResolvedTargetSnapshot { get; }
    public string CauseText { get; }
    public string? CustomText { get; }
    public DeathNoteEntryType EntryType { get; }
    public ProtoId<DeathNotePresetPrototype>? PresetId { get; }
    public TimeSpan CreatedRoundTime { get; }
    public TimeSpan ScheduledRoundTime { get; }
    public bool MakeTargetUnrevivable { get; }
    public DeathNoteEntryStatus Status { get; internal set; }
    public DeathNoteFailureReason FailureReason { get; internal set; }
    public string? Result { get; internal set; }
    public string? Error { get; internal set; }

    public DeathNoteEntry(
        uint entryId,
        EntityUid notebookEntity,
        EntityUid? ownerEntity,
        EntityUid writerEntity,
        int pageIndex,
        int lineIndex,
        EntityUid? targetEntity,
        string notebookSnapshot,
        string ownerSnapshot,
        string writerSnapshot,
        string originalText,
        string enteredTargetName,
        string resolvedTargetSnapshot,
        string causeText,
        string? customText,
        DeathNoteEntryType entryType,
        ProtoId<DeathNotePresetPrototype>? presetId,
        TimeSpan createdRoundTime,
        TimeSpan scheduledRoundTime,
        bool makeTargetUnrevivable,
        DeathNoteEntryStatus status,
        DeathNoteFailureReason failureReason = DeathNoteFailureReason.None,
        string? result = null,
        string? error = null)
    {
        EntryId = entryId;
        NotebookEntity = notebookEntity;
        OwnerEntity = ownerEntity;
        WriterEntity = writerEntity;
        PageIndex = pageIndex;
        LineIndex = lineIndex;
        TargetEntity = targetEntity;
        NotebookSnapshot = notebookSnapshot;
        OwnerSnapshot = ownerSnapshot;
        WriterSnapshot = writerSnapshot;
        OriginalText = originalText;
        EnteredTargetName = enteredTargetName;
        ResolvedTargetSnapshot = resolvedTargetSnapshot;
        CauseText = causeText;
        CustomText = customText;
        EntryType = entryType;
        PresetId = presetId;
        CreatedRoundTime = createdRoundTime;
        ScheduledRoundTime = scheduledRoundTime;
        MakeTargetUnrevivable = makeTargetUnrevivable;
        Status = status;
        FailureReason = failureReason;
        Result = result;
        Error = error;
    }
}
