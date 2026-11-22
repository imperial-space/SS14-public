using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.DeimonFly.DeathNote;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Центральный серверный журнал текущего раунда.
/// Работает событийно, а изменяемое состояние хранит на отдельной служебной сущности.
/// </summary>
public sealed class DeathNoteJournalSystem : EntitySystem
{
    [ViewVariables]
    public int EntryCount => GetRuntime().Comp.EntryNodes.Count;

    [ViewVariables]
    public int RejectedEntryLimit => Math.Max(0, GetRuntime().Comp.MaxRejectedEntries);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public uint AllocateEntryId()
    {
        var runtime = GetRuntime().Comp;
        if (runtime.NextEntryId == 0)
            runtime.NextEntryId = 1;

        return runtime.NextEntryId++;
    }

    public bool AddEntry(DeathNoteEntry entry)
    {
        var runtime = GetRuntime().Comp;
        if (runtime.EntryNodes.ContainsKey(entry.EntryId))
            return false;

        var rejected = IsRejectedAttempt(entry);
        if (rejected && !MakeRoomForRejectedEntry(runtime))
            return false;

        var node = runtime.OrderedEntries.AddLast(entry);
        runtime.EntryNodes.Add(entry.EntryId, node);
        if (rejected)
            runtime.RejectedEntryIds.Enqueue(entry.EntryId);

        return true;
    }

    public bool TryGetEntry(uint entryId, out DeathNoteEntry? entry)
    {
        var runtime = GetRuntime().Comp;
        if (runtime.EntryNodes.TryGetValue(entryId, out var node))
        {
            entry = node.Value;
            return true;
        }

        entry = null;
        return false;
    }

    public IEnumerable<DeathNoteEntry> EnumerateEntries()
    {
        return GetRuntime().Comp.OrderedEntries;
    }

    /// <summary>
    /// Возвращает одну страницу от новых записей к старым без копирования и разворота всего журнала.
    /// </summary>
    public IReadOnlyList<DeathNoteEntry> GetReversePage(int pageIndex, int pageSize)
    {
        var runtime = GetRuntime().Comp;
        var safePage = Math.Max(0, pageIndex);
        var safePageSize = Math.Max(1, pageSize);
        var entriesToSkip = (long) safePage * safePageSize;
        var current = runtime.OrderedEntries.Last;

        while (current != null && entriesToSkip > 0)
        {
            current = current.Previous;
            entriesToSkip--;
        }

        var page = new List<DeathNoteEntry>(safePageSize);
        while (current != null && page.Count < safePageSize)
        {
            page.Add(current.Value);
            current = current.Previous;
        }

        return page;
    }

    public bool TryUpdateStatus(
        uint entryId,
        DeathNoteEntryStatus status,
        DeathNoteFailureReason failureReason = DeathNoteFailureReason.None,
        string? result = null,
        string? error = null)
    {
        var runtime = GetRuntime().Comp;
        if (!runtime.EntryNodes.TryGetValue(entryId, out var node))
            return false;

        var entry = node.Value;
        if (!DeathNoteStatusTransitions.CanTransition(entry.Status, status))
            return false;

        entry.Status = status;
        entry.FailureReason = failureReason;
        entry.Result = result;
        entry.Error = error;
        return true;
    }

    /// <summary>
    /// Очищает журнал текущего раунда.
    /// </summary>
    public void ClearRound()
    {
        var runtime = GetRuntime().Comp;
        runtime.EntryNodes.Clear();
        runtime.OrderedEntries.Clear();
        runtime.RejectedEntryIds.Clear();
        runtime.NextEntryId = 1;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        ClearRound();
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        // Новый раунд получает отдельное пространство EntryId даже после мягкого перезапуска.
        ClearRound();
    }

    private Entity<DeathNoteJournalRuntimeComponent> GetRuntime()
    {
        var query = EntityQueryEnumerator<DeathNoteJournalRuntimeComponent>();
        if (query.MoveNext(out var uid, out var runtime))
            return (uid, runtime);

        var runtimeUid = Spawn(DeathNoteSchedulerRuntimeComponent.Prototype);
        return (runtimeUid, Comp<DeathNoteJournalRuntimeComponent>(runtimeUid));
    }

    private static bool IsRejectedAttempt(DeathNoteEntry entry)
    {
        return entry.PageIndex < 0 || entry.LineIndex < 0;
    }

    private static bool MakeRoomForRejectedEntry(DeathNoteJournalRuntimeComponent runtime)
    {
        var limit = Math.Max(0, runtime.MaxRejectedEntries);
        if (limit == 0)
            return false;

        while (runtime.RejectedEntryIds.Count >= limit)
        {
            var oldestRejectedId = runtime.RejectedEntryIds.Dequeue();
            if (!runtime.EntryNodes.Remove(oldestRejectedId, out var node))
                continue;

            runtime.OrderedEntries.Remove(node);
        }

        return true;
    }
}
