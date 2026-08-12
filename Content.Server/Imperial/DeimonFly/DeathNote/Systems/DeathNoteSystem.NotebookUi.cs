using System.Collections.Immutable;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Models;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Interaction;
using Content.Shared.UserInterface;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteSystem
{
    private void OnNotebookInteractUsing(Entity<DeathNoteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tags.HasTag(args.Used, ent.Comp.WritingTag))
            return;

        // Чтение пустой рукой обрабатывает ActivatableUI. InteractUsing лишь добавляет открытие
        // письменными принадлежностями и не перехватывает взаимодействия прочих предметов.
        args.Handled = _ui.TryOpenUi(ent.Owner, DeathNoteUiKey.Notebook, args.User);
    }

    private void OnNotebookUiAttempt(Entity<DeathNoteComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!args.UiKey.Equals(DeathNoteUiKey.Notebook))
            return;

        if (CanAccessNotebook(args.Actor, ent.Owner))
            return;

        args.Cancel();
        _ui.CloseUi(ent.Owner, DeathNoteUiKey.Notebook, args.Actor);
        var runtime = EnsureComp<DeathNoteRuntimeComponent>(ent.Owner);
        if (_timing.CurTime < runtime.NextInvalidAttemptLogTime)
            return;

        runtime.NextInvalidAttemptLogTime = _timing.CurTime + ent.Comp.InvalidAttemptLogCooldown;
        _adminLog.Add(
            LogType.Action,
            LogImpact.Low,
            $"{args.Actor:player} attempted an invalid Death Note BUI interaction with {ent.Owner:entity}.");
    }

    private void OnNotebookOpened(EntityUid uid, DeathNoteComponent component, BoundUIOpenedEvent args)
    {
        var runtime = EnsureComp<DeathNoteRuntimeComponent>(uid);
        UpdateNotebookState(uid, component, runtime, args.Actor, 0);
    }

    private void OnSpreadRequested(
        EntityUid uid,
        DeathNoteComponent component,
        DeathNoteSpreadRequestMessage message)
    {
        var actor = message.Actor;
        if (!_ui.IsUiOpen(uid, DeathNoteUiKey.Notebook, actor) ||
            !CanAccessNotebook(actor, uid))
        {
            _ui.CloseUi(uid, DeathNoteUiKey.Notebook, actor);
            return;
        }

        var runtime = EnsureComp<DeathNoteRuntimeComponent>(uid);
        UpdateNotebookState(uid, component, runtime, actor, message.SpreadIndex);
    }

    private bool CanAccessNotebook(EntityUid actor, EntityUid notebook)
    {
        if (Deleted(actor) || Deleted(notebook))
            return false;

        return _interaction.InRangeAndAccessible(actor, notebook);
    }

    private bool CanWrite(EntityUid actor, EntityUid notebook, DeathNoteComponent component)
    {
        if (!TryComp(actor, out HandsComponent? hands))
            return false;

        foreach (var heldItem in _hands.EnumerateHeld((actor, hands)))
        {
            if (heldItem != notebook && _tags.HasTag(heldItem, component.WritingTag))
                return true;
        }

        return false;
    }

    private void Accept(
        EntityUid notebook,
        DeathNoteComponent component,
        DeathNoteRuntimeComponent runtime,
        EntityUid actor,
        int pageIndex)
    {
        _ui.ServerSendUiMessage(
            notebook,
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmissionResponseMessage(DeathNoteSubmissionFeedback.Accepted, runtime.SubmissionRevision),
            actor);
        UpdateNotebookState(
            notebook,
            component,
            runtime,
            actor,
            GetSpreadIndexForPageOrFirst(pageIndex, component));
    }

    private void Reject(
        EntityUid notebook,
        DeathNoteComponent component,
        DeathNoteRuntimeComponent runtime,
        EntityUid actor,
        DeathNoteSubmitMessage message,
        DeathNoteSubmissionFeedback feedback,
        DeathNoteFailureReason reason)
    {
        RecordRejectedAttempt(notebook, component, runtime, actor, message, reason);

        _adminLog.Add(
            LogType.Action,
            LogImpact.Low,
            $"{actor:player} had a Death Note submission rejected on {notebook:entity}: {reason}; " +
            $"line='{ClampAudit(message.EntryText)}'.");

        _ui.ServerSendUiMessage(
            notebook,
            DeathNoteUiKey.Notebook,
            new DeathNoteSubmissionResponseMessage(feedback, runtime.SubmissionRevision),
            actor);
        UpdateNotebookState(
            notebook,
            component,
            runtime,
            actor,
            GetSpreadIndexForPageOrFirst(message.PageIndex, component));
    }

    private void UpdateNotebookState(
        EntityUid notebook,
        DeathNoteComponent component,
        DeathNoteRuntimeComponent runtime,
        EntityUid actor,
        int requestedSpreadIndex)
    {
        var writablePageCount = GetWritablePageCount(component);
        var entriesPerPage = GetEntriesPerPage(component);
        var spreadCount = DeathNotePagination.GetSpreadCount(writablePageCount);
        var spreadIndex = DeathNotePagination.ClampSpreadIndex(requestedSpreadIndex, spreadCount);
        var spread = DeathNotePagination.GetSpread(spreadIndex, writablePageCount);
        var leftPageIndex = spread.Left.Kind == DeathNoteNotebookSheetKind.Writable
            ? spread.Left.PageIndex
            : -1;
        var rightPageIndex = spread.Right.Kind == DeathNoteNotebookSheetKind.Writable
            ? spread.Right.PageIndex
            : -1;
        var visiblePageCount = (leftPageIndex >= 0 ? 1 : 0) + (rightPageIndex >= 0 ? 1 : 0);
        var entryCapacity = (int) Math.Min(
            runtime.EntryIds.Count,
            Math.Min(int.MaxValue, (long) entriesPerPage * visiblePageCount));
        var entries = ImmutableArray.CreateBuilder<DeathNotePublicEntry>(entryCapacity);
        var leftEntryCount = 0;
        var rightEntryCount = 0;

        foreach (var entryId in runtime.EntryIds)
        {
            if (!_journal.TryGetEntry(entryId, out var entry) || entry == null)
                continue;

            if (entry.PageIndex == leftPageIndex)
            {
                if (leftEntryCount >= entriesPerPage)
                    continue;

                leftEntryCount++;
            }
            else if (entry.PageIndex == rightPageIndex)
            {
                if (rightEntryCount >= entriesPerPage)
                    continue;

                rightEntryCount++;
            }
            else
            {
                continue;
            }

            entries.Add(new DeathNotePublicEntry(
                entry.PageIndex,
                entry.LineIndex,
                entry.OriginalText));

            if (entries.Count >= entryCapacity)
                break;
        }

        var maxEntries = GetEffectiveMaxEntries(component);
        var canSubmit = _gameTicker.RunLevel == GameRunLevel.InRound &&
                        runtime.EntryIds.Count < maxEntries &&
                        CanAccessNotebook(actor, notebook) &&
                        CanWrite(actor, notebook, component);

        _ui.ServerSendUiMessage(
            notebook,
            DeathNoteUiKey.Notebook,
            new DeathNoteNotebookStateMessage(
                new DeathNoteBoundUserInterfaceState(
                    component.RulePage,
                    entries.ToImmutable(),
                    spreadIndex,
                    component.MaxEntryTextLength,
                    writablePageCount,
                    entriesPerPage,
                    runtime.SubmissionRevision,
                    canSubmit)),
            actor);
    }

    private static int GetSpreadIndexForPageOrFirst(int pageIndex, DeathNoteComponent component)
    {
        return Math.Max(0, DeathNotePagination.GetSpreadIndexForPage(pageIndex, GetWritablePageCount(component)));
    }

    private int GetPageEntryCount(DeathNoteRuntimeComponent runtime, int pageIndex)
    {
        var count = 0;
        foreach (var entryId in runtime.EntryIds)
        {
            if (_journal.TryGetEntry(entryId, out var entry) && entry?.PageIndex == pageIndex)
                count++;
        }

        return count;
    }

    private static int GetWritablePageCount(DeathNoteComponent component)
    {
        return Math.Max(1, component.WritablePageCount);
    }

    private static int GetEntriesPerPage(DeathNoteComponent component)
    {
        return Math.Max(1, component.EntriesPerPage);
    }

    private static int GetEffectiveMaxEntries(DeathNoteComponent component)
    {
        var pageCapacity = (long) GetWritablePageCount(component) * GetEntriesPerPage(component);
        var configuredMaximum = Math.Max(1, component.MaxEntries);
        return (int) Math.Min(configuredMaximum, Math.Min(int.MaxValue, pageCapacity));
    }
}
