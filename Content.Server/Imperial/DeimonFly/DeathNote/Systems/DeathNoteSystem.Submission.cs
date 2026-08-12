using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteSystem
{
    private void OnSubmit(EntityUid uid, DeathNoteComponent component, DeathNoteSubmitMessage message)
    {
        var actor = message.Actor;
        var runtime = EnsureComp<DeathNoteRuntimeComponent>(uid);
        var now = _gameTicker.RunLevel == GameRunLevel.InRound
            ? _gameTicker.RoundDuration()
            : TimeSpan.Zero;

        if (!_ui.IsUiOpen(uid, DeathNoteUiKey.Notebook, actor) ||
            !CanAccessNotebook(actor, uid))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.AccessDenied,
                DeathNoteFailureReason.AccessDenied);
            return;
        }

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.AccessDenied,
                DeathNoteFailureReason.RoundEnded);
            return;
        }

        if (now < runtime.NextSubmissionTime)
        {
            // Не повторяем дорогую валидацию, полное состояние и административный лог для сетевого спама.
            _ui.ServerSendUiMessage(
                uid,
                DeathNoteUiKey.Notebook,
                new DeathNoteSubmissionResponseMessage(
                    DeathNoteSubmissionFeedback.Cooldown,
                    runtime.SubmissionRevision),
                actor);
            return;
        }

        runtime.NextSubmissionTime = now + component.SubmissionCooldown;

        if (!CanWrite(actor, uid, component))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.NoPen,
                DeathNoteFailureReason.NoPen);
            return;
        }

        if (message.Revision != runtime.SubmissionRevision)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.Replay,
                DeathNoteFailureReason.Replay);
            return;
        }

        if ((message.EntryText?.Length ?? 0) > component.MaxEntryTextLength)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.CauseTooLong,
                DeathNoteFailureReason.CauseTooLong);
            return;
        }

        if (!DeathNoteInputParser.TryParseLine(message.EntryText, out var parsedLine))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.InvalidFormat,
                DeathNoteFailureReason.InvalidFormat);
            return;
        }

        if (parsedLine.TargetName.Length > component.MaxNameLength)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.NameTooLong,
                DeathNoteFailureReason.NameTooLong);
            return;
        }

        if (parsedLine.CauseText.Length > component.MaxCauseLength)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.CauseTooLong,
                DeathNoteFailureReason.CauseTooLong);
            return;
        }

        if (parsedLine.ExecutionTime.Length > component.MaxTimeLength)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.InvalidTime,
                DeathNoteFailureReason.InvalidTime);
            return;
        }

        var targetName = parsedLine.TargetName;
        if (targetName.Length == 0)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.InvalidName,
                DeathNoteFailureReason.InvalidName);
            return;
        }

        var writablePageCount = GetWritablePageCount(component);
        if (message.PageIndex < 0 || message.PageIndex >= writablePageCount)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.InvalidPage,
                DeathNoteFailureReason.InvalidPage);
            return;
        }

        var entriesPerPage = GetEntriesPerPage(component);
        var lineIndex = GetPageEntryCount(runtime, message.PageIndex);
        if (lineIndex >= entriesPerPage)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.PageFull,
                DeathNoteFailureReason.PageFull);
            return;
        }

        if (runtime.EntryIds.Count >= GetEffectiveMaxEntries(component))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.EntryLimitReached,
                DeathNoteFailureReason.EntryLimitReached);
            return;
        }

        if (!DeathNoteInputParser.TryParseCause(parsedLine.CauseText, out var parsedCause))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.UnknownPreset,
                DeathNoteFailureReason.UnknownPreset);
            return;
        }

        TimeSpan scheduledTime;
        if (parsedCause.EntryType == DeathNoteEntryType.Custom)
        {
            // Время в пользовательском ролевом предписании — часть текста для цели.
            // Такое влияние доставляется немедленно и не отклоняется, даже если указанное
            // время уже прошло относительно текущего времени раунда.
            scheduledTime = now;
        }
        else if (!DeathNoteInputValidator.TryResolveScheduledTime(
                     now,
                     parsedLine.ExecutionTime,
                     component.DefaultDelay,
                     out scheduledTime,
                     out var timeFailure))
        {
            var feedback = timeFailure == DeathNoteFailureReason.TimeInPast
                ? DeathNoteSubmissionFeedback.TimeInPast
                : DeathNoteSubmissionFeedback.InvalidTime;
            Reject(uid, component, runtime, actor, message, feedback, timeFailure);
            return;
        }

        DeathNotePresetPrototype? preset = null;
        if (parsedCause.EntryType == DeathNoteEntryType.Standard)
        {
            if (!_prototypeManager.TryIndex(component.DefaultPreset, out preset) || !preset.Enabled)
            {
                Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.TechnicalError,
                    DeathNoteFailureReason.TechnicalError);
                return;
            }
        }
        else if (parsedCause.EntryType == DeathNoteEntryType.Preset &&
                 !_presetRegistry.TryResolve(parsedCause.CauseText, out preset))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.UnknownPreset,
                DeathNoteFailureReason.UnknownPreset);
            return;
        }

        if (preset != null && preset.Parameters.MinimumExecutionDelay < TimeSpan.Zero)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.TechnicalError,
                DeathNoteFailureReason.TechnicalError);
            return;
        }

        if (preset != null &&
            string.IsNullOrWhiteSpace(parsedLine.ExecutionTime) &&
            preset.Parameters.MinimumExecutionDelay > component.DefaultDelay)
        {
            scheduledTime = now + preset.Parameters.MinimumExecutionDelay;
        }
        else if (preset != null &&
                 !string.IsNullOrWhiteSpace(parsedLine.ExecutionTime) &&
                 scheduledTime - now < preset.Parameters.MinimumExecutionDelay)
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.TimeTooSoon,
                DeathNoteFailureReason.TimeTooSoon);
            return;
        }

        var targetResolution = ResolveTarget(targetName);
        var entryId = _journal.AllocateEntryId();
        var originalText = DeathNoteInputParser.NormalizeWrittenText(message.EntryText);
        var entry = CreateEntry(
            entryId,
            uid,
            runtime,
            actor,
            message.PageIndex,
            lineIndex,
            targetResolution.Target,
            originalText,
            targetName,
            parsedCause,
            preset,
            now,
            scheduledTime);

        if (!_journal.AddEntry(entry))
        {
            Reject(uid, component, runtime, actor, message, DeathNoteSubmissionFeedback.TechnicalError,
                DeathNoteFailureReason.TechnicalError);
            return;
        }

        // С этого момента запись подтверждена и навсегда занимает строку тетради.
        runtime.EntryIds.Add(entryId);
        runtime.SubmissionRevision = NextRevision(runtime.SubmissionRevision);

        if (targetResolution.Target == null)
        {
            _journal.TryUpdateStatus(
                entryId,
                targetResolution.Status,
                targetResolution.FailureReason,
                result: "No automatic effect was scheduled.");
            LogConfirmedEntry(entry, targetResolution.FailureReason);
            Accept(uid, component, runtime, actor, message.PageIndex);
            return;
        }

        if (parsedCause.EntryType == DeathNoteEntryType.Custom)
        {
            DeliverCustomScenario(entry, entry.CustomText ?? string.Empty, targetResolution.Target.Value);
            LogConfirmedEntry(entry, entry.FailureReason);
            Accept(uid, component, runtime, actor, message.PageIndex);
            return;
        }

        if (!_journal.TryUpdateStatus(entryId, DeathNoteEntryStatus.Scheduled,
                result: "Waiting for the configured round time."))
        {
            _journal.TryUpdateStatus(entryId, DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.TechnicalError,
                error: "Could not move the entry to Scheduled.");
        }
        else if (!_presetRegistry.HasHandler(preset!.Handler) ||
                 !TrySchedulePreset(entryId, preset, scheduledTime - now))
        {
            _journal.TryUpdateStatus(entryId, DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.TechnicalError,
                error: "The round timer rejected the entry.");
        }

        LogConfirmedEntry(entry, entry.FailureReason);
        Accept(uid, component, runtime, actor, message.PageIndex);
    }

    private TargetResolution ResolveTarget(string targetName)
    {
        var normalizedTargetName = DeathNoteInputParser.NormalizeCause(targetName);
        EntityUid? livingTarget = null;
        var matchingHumanoidFound = false;
        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            // «Е» и «ё» равнозначны только при поиске; авторское написание в записи сохраняется.
            var baseName = DeathNoteInputParser.NormalizeCause(_nameModifier.GetBaseName(uid));
            if (!string.Equals(baseName, normalizedTargetName, StringComparison.OrdinalIgnoreCase))
                continue;

            matchingHumanoidFound = true;
            if (_mobState.IsDead(uid, mobState))
                continue;

            if (livingTarget != null)
            {
                return new TargetResolution(
                    null,
                    DeathNoteEntryStatus.AmbiguousName,
                    DeathNoteFailureReason.AmbiguousName);
            }

            livingTarget = uid;
        }

        if (livingTarget != null)
            return new TargetResolution(livingTarget, DeathNoteEntryStatus.Submitted, DeathNoteFailureReason.None);

        return matchingHumanoidFound
            ? new TargetResolution(null, DeathNoteEntryStatus.TargetAlreadyDead, DeathNoteFailureReason.TargetAlreadyDead)
            : new TargetResolution(null, DeathNoteEntryStatus.TargetMissing, DeathNoteFailureReason.TargetMissing);
    }

    private DeathNoteEntry CreateEntry(
        uint entryId,
        EntityUid notebook,
        DeathNoteRuntimeComponent runtime,
        EntityUid writer,
        int pageIndex,
        int lineIndex,
        EntityUid? target,
        string originalText,
        string targetName,
        DeathNoteParsedCause parsedCause,
        DeathNotePresetPrototype? preset,
        TimeSpan createdTime,
        TimeSpan scheduledTime)
    {
        return new DeathNoteEntry(
            entryId,
            notebook,
            runtime.OwnerEntity,
            writer,
            pageIndex,
            lineIndex,
            target,
            Snapshot(notebook),
            Snapshot(runtime.OwnerEntity),
            Snapshot(writer),
            originalText,
            targetName,
            Snapshot(target),
            parsedCause.CauseText,
            parsedCause.CustomText,
            parsedCause.EntryType,
            preset?.ID,
            createdTime,
            scheduledTime,
            TryComp(notebook, out DeathNoteComponent? component) && component.MakeTargetsUnrevivable,
            DeathNoteEntryStatus.Submitted);
    }

    private void RecordRejectedAttempt(
        EntityUid notebook,
        DeathNoteComponent component,
        DeathNoteRuntimeComponent runtime,
        EntityUid actor,
        DeathNoteSubmitMessage message,
        DeathNoteFailureReason reason)
    {
        if (!ShouldJournalRejectedAttempt(reason))
            return;

        var now = _gameTicker.RunLevel == GameRunLevel.InRound
            ? _gameTicker.RoundDuration()
            : TimeSpan.Zero;
        var parsedLineValid = DeathNoteInputParser.TryParseLine(message.EntryText, out var parsedLine);
        var targetName = parsedLineValid ? parsedLine.TargetName : string.Empty;
        var causeText = parsedLineValid ? parsedLine.CauseText : DeathNoteInputParser.NormalizeCause(message.EntryText);

        DeathNoteEntryType entryType;
        string? customText = null;
        if (DeathNoteInputParser.TryParseCause(causeText, out var parsedCause))
        {
            entryType = parsedCause.EntryType;
            customText = parsedCause.CustomText;
        }
        else
        {
            entryType = causeText.StartsWith('!')
                ? DeathNoteEntryType.Custom
                : DeathNoteEntryType.Preset;
        }

        ProtoId<DeathNotePresetPrototype>? presetId = null;
        if (entryType == DeathNoteEntryType.Standard)
        {
            presetId = component.DefaultPreset;
        }
        else if (reason != DeathNoteFailureReason.UnknownPreset &&
                 entryType == DeathNoteEntryType.Preset &&
                 _presetRegistry.TryResolve(causeText, out var resolvedPreset) &&
                 resolvedPreset != null)
        {
            presetId = resolvedPreset.ID;
        }

        var scheduledTime = TimeSpan.Zero;
        if (!parsedLineValid || string.IsNullOrWhiteSpace(parsedLine.ExecutionTime))
            scheduledTime = now + component.DefaultDelay;
        else
            DeathNoteInputParser.TryParseRoundTime(parsedLine.ExecutionTime, out scheduledTime);

        var status = reason is DeathNoteFailureReason.EntryLimitReached or DeathNoteFailureReason.TechnicalError
            ? DeathNoteEntryStatus.Failed
            : DeathNoteEntryStatus.InvalidFormat;
        var entryId = _journal.AllocateEntryId();
        var entry = new DeathNoteEntry(
            entryId,
            notebook,
            runtime.OwnerEntity,
            actor,
            -1,
            -1,
            null,
            Snapshot(notebook),
            Snapshot(runtime.OwnerEntity),
            Snapshot(actor),
            DeathNoteInputParser.NormalizeWrittenText(message.EntryText),
            ClampAudit(targetName),
            string.Empty,
            ClampAudit(causeText),
            customText == null ? null : ClampAudit(customText),
            entryType,
            presetId,
            now,
            scheduledTime,
            false,
            status,
            reason,
            error: $"Submission was rejected before target resolution: {reason}.");

        _journal.AddEntry(entry);
    }

    private static bool ShouldJournalRejectedAttempt(DeathNoteFailureReason reason)
    {
        return reason is
            DeathNoteFailureReason.InvalidName or
            DeathNoteFailureReason.InvalidFormat or
            DeathNoteFailureReason.InvalidTime or
            DeathNoteFailureReason.TimeInPast or
            DeathNoteFailureReason.TimeTooSoon or
            DeathNoteFailureReason.UnknownPreset or
            DeathNoteFailureReason.NameTooLong or
            DeathNoteFailureReason.CauseTooLong or
            DeathNoteFailureReason.InvalidPage or
            DeathNoteFailureReason.PageFull or
            DeathNoteFailureReason.EntryLimitReached or
            DeathNoteFailureReason.TechnicalError;
    }

    private readonly record struct TargetResolution(
        EntityUid? Target,
        DeathNoteEntryStatus Status,
        DeathNoteFailureReason FailureReason);
}
