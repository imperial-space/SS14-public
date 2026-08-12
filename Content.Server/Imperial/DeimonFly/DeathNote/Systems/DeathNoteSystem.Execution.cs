using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Server.Imperial.DeimonFly.DeathNote.Models;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Imperial.DeimonFly.DeathNote.Scheduling;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Traits.Assorted;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteSystem
{
    private void OnGuidedEffectStart(
        Entity<DeathNoteGuidedScenarioComponent> ent,
        ref DeathNoteGuidedEffectStartEvent args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound ||
            args.EntryId != ent.Comp.EntryId ||
            !_journal.TryGetEntry(args.EntryId, out var entry) ||
            entry == null ||
            entry.TargetEntity != ent.Owner)
        {
            return;
        }

        // Persistent guided scenarios may be triggered long after their assigned
        // execution time. Refresh tracking when the actual world interaction
        // begins so the resulting death is still attributed to this entry.
        if (entry.Status == DeathNoteEntryStatus.EffectStarted)
        {
            var refreshedUnrevivable = ApplyUnrevivablePolicy(entry, ent.Owner);
            TrackStartedEffect(
                entry,
                ent.Owner,
                refreshedUnrevivable,
                ent.Comp.EffectTrackingDuration);
            args.Prepared = true;
            return;
        }

        if (entry.Status != DeathNoteEntryStatus.Scheduled ||
            !TryComp(ent.Owner, out MobStateComponent? mobState))
        {
            return;
        }

        if (_mobState.IsDead(ent.Owner, mobState))
        {
            _journal.TryUpdateStatus(
                entry.EntryId,
                DeathNoteEntryStatus.TargetAlreadyDead,
                DeathNoteFailureReason.TargetNoLongerAlive,
                result: "Target was already dead when the guided effect was triggered.");
            LogExecutionResult(entry, false, "target was already dead before the guided effect");
            return;
        }

        if (!_journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Executing))
            return;

        var addedUnrevivable = ApplyUnrevivablePolicy(entry, ent.Owner);
        if (!_journal.TryUpdateStatus(
                entry.EntryId,
                DeathNoteEntryStatus.EffectStarted,
                result: $"Guided effect {ent.Comp.Scenario} was triggered by the target."))
        {
            if (addedUnrevivable)
                RemCompDeferred<UnrevivableComponent>(ent.Owner);

            _journal.TryUpdateStatus(
                entry.EntryId,
                DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.TechnicalError,
                error: "Could not record the guided effect start.");
            return;
        }

        TrackStartedEffect(
            entry,
            ent.Owner,
            addedUnrevivable,
            ent.Comp.EffectTrackingDuration);
        args.Prepared = true;

        _adminLog.Add(
            LogType.Action,
            LogImpact.Extreme,
            $"Death Note entry #{entry.EntryId} started guided effect {ent.Comp.Scenario} " +
            $"for {ent.Owner:player}.");
    }

    private void OnScheduledEntry(DeathNoteScheduledEntryEvent args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound || args.RoundId != _gameTicker.RoundId ||
            !_journal.TryGetEntry(args.EntryId, out var entry) || entry == null ||
            entry.Status != DeathNoteEntryStatus.Scheduled)
        {
            return;
        }

        if (entry.TargetEntity is not { } target || Deleted(target))
        {
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.TargetDeleted,
                error: "Target entity was deleted before execution.");
            LogExecutionResult(entry, false, "target deleted");
            return;
        }

        if (!TryComp(target, out MobStateComponent? mobState) || _mobState.IsDead(target, mobState))
        {
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.TargetAlreadyDead,
                DeathNoteFailureReason.TargetNoLongerAlive,
                result: "Target was already dead at execution time.");
            LogExecutionResult(entry, false, "target already dead");
            return;
        }

        if (entry.EntryType == DeathNoteEntryType.Custom)
        {
            DeliverCustomScenario(entry, entry.CustomText ?? string.Empty, target);
            return;
        }

        if (entry.PresetId is not { } presetId ||
            !_prototypeManager.TryIndex(presetId, out DeathNotePresetPrototype? preset) ||
            !_presetRegistry.HasHandler(preset.Handler))
        {
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.HandlerError,
                error: "Preset or handler was unavailable at execution time.");
            LogExecutionResult(entry, false, "handler unavailable");
            return;
        }

        if (preset.Parameters.EffectTrackingDuration <= TimeSpan.Zero)
        {
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.HandlerError,
                error: "Preset effect tracking duration must be positive.");
            LogExecutionResult(entry, false, "invalid effect tracking duration");
            return;
        }

        var context = new DeathNotePresetExecutionContext(
            entry.EntryId,
            entry.NotebookEntity,
            entry.OwnerEntity,
            entry.WriterEntity,
            target,
            entry.EnteredTargetName);

        if (args.Phase == DeathNoteScheduledPhase.Prelude)
        {
            BeginPrelude(entry, preset, context);
            return;
        }

        if (!_journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Executing))
            return;

        var addedUnrevivable = ApplyUnrevivablePolicy(entry, target);

        _adminLog.Add(
            LogType.Action,
            LogImpact.Extreme,
            $"Death Note entry #{entry.EntryId} started handler {preset.Handler} for {target:player}.");

        DeathNotePresetExecutionResult result;
        try
        {
            if (!_presetRegistry.TryExecute(
                    preset.Handler,
                    context,
                    preset.Parameters,
                    out result))
            {
                result = DeathNotePresetExecutionResult.Failed(
                    "Preset handler was unavailable at execution time.");
            }
        }
        catch (Exception exception)
        {
            result = DeathNotePresetExecutionResult.Failed(exception.ToString());
        }

        var deathConfirmed = !Deleted(target) && _mobState.IsDead(target);
        if (deathConfirmed)
        {
            var resultText = result.Success
                ? $"{result.Result} Target death was confirmed."
                : $"Handler reported failure, but target death was confirmed. {result.Error}";
            _journal.TryUpdateStatus(
                entry.EntryId,
                DeathNoteEntryStatus.DeathConfirmed,
                result: ClampAudit(resultText));
        }
        else if (result.Success)
        {
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.EffectStarted,
                result: result.Result);
            TrackStartedEffect(
                entry,
                target,
                addedUnrevivable,
                preset.Parameters.EffectTrackingDuration);
        }
        else
        {
            if (addedUnrevivable)
                RemCompDeferred<UnrevivableComponent>(target);
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Failed,
                DeathNoteFailureReason.HandlerError,
                error: ClampAudit(result.Error));
        }

        LogExecutionResult(
            entry,
            result.Success || deathConfirmed,
            deathConfirmed && !result.Success
                ? $"handler reported failure, but target death was confirmed: {result.Error}"
                : result.Result ?? result.Error ?? string.Empty);
    }

    private bool TrySchedulePreset(
        uint entryId,
        DeathNotePresetPrototype preset,
        TimeSpan executionDelay)
    {
        var effectiveExecutionDelay = DeathNoteScheduleMath.GetEffectiveExecutionDelay(
            executionDelay,
            preset.Parameters.ExecutionAdvance);

        if (_presetRegistry.TryGetPreludeDuration(
                preset.Handler,
                preset.Parameters,
                out var duration))
        {
            if (duration > TimeSpan.Zero)
            {
                var preludeDelay = DeathNoteScheduleMath.GetPreludeDelay(
                    effectiveExecutionDelay,
                    duration);
                if (!_scheduler.TrySchedule(entryId, DeathNoteScheduledPhase.Prelude, preludeDelay))
                    return false;
            }
        }

        return _scheduler.TrySchedule(entryId, DeathNoteScheduledPhase.Execution, effectiveExecutionDelay);
    }

    private void BeginPrelude(
        DeathNoteEntry entry,
        DeathNotePresetPrototype preset,
        in DeathNotePresetExecutionContext context)
    {
        DeathNotePresetExecutionResult result;
        try
        {
            if (!_presetRegistry.TryBeginPrelude(
                    preset.Handler,
                    context,
                    preset.Parameters,
                    out result))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            result = DeathNotePresetExecutionResult.Failed(exception.ToString());
        }

        _adminLog.Add(
            LogType.Action,
            result.Success ? LogImpact.High : LogImpact.Medium,
            $"Death Note entry #{entry.EntryId} prelude {(result.Success ? "started" : "failed")}: " +
            $"{ClampAudit(result.Result ?? result.Error)}.");
    }

    private void OnTimersCancelled(DeathNoteTimersCancelledEvent args)
    {
        foreach (var entryId in args.EntryIds)
        {
            if (_journal.TryUpdateStatus(entryId, DeathNoteEntryStatus.Cancelled,
                    DeathNoteFailureReason.RoundEnded,
                    result: "Pending timer was cancelled at round end."))
            {
                _adminLog.Add(LogType.Action, LogImpact.Low,
                    $"Death Note entry #{entryId} was cancelled because the round ended.");
            }
        }
    }

    private bool ApplyUnrevivablePolicy(DeathNoteEntry entry, EntityUid target)
    {
        if (!entry.MakeTargetUnrevivable || Deleted(target))
            return false;

        // Чужую политику реанимации нельзя перезаписывать или впоследствии удалять.
        if (HasComp<UnrevivableComponent>(target))
            return false;

        var unrevivable = EnsureComp<UnrevivableComponent>(target);
        unrevivable.Analyzable = false;
        unrevivable.Cloneable = false;
        unrevivable.ReasonMessage = UnrevivableReason;
        Dirty(target, unrevivable);
        return true;
    }

    private void TrackStartedEffect(
        DeathNoteEntry entry,
        EntityUid target,
        bool addedUnrevivable,
        TimeSpan trackingDuration)
    {
        if (trackingDuration <= TimeSpan.Zero)
            return;

        var tracker = EnsureComp<DeathNoteTargetTrackingComponent>(target);
        var deadline = _timing.CurTime + trackingDuration;
        tracker.EntryDeadlines[entry.EntryId] = deadline;
        tracker.NextExpiry = tracker.NextExpiry == TimeSpan.Zero
            ? deadline
            : TimeSpan.FromTicks(Math.Min(tracker.NextExpiry.Ticks, deadline.Ticks));

        if (entry.MakeTargetUnrevivable)
            tracker.UnrevivableEntryIds.Add(entry.EntryId);

        tracker.OwnsUnrevivableComponent |= addedUnrevivable;
    }

    private void OnTrackedTargetStateChanged(
        Entity<DeathNoteTargetTrackingComponent> ent,
        ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        ConfirmTrackedDeath(ent);
    }

    private void OnTrackedTargetGibbed(
        Entity<DeathNoteTargetTrackingComponent> ent,
        ref GibbedBeforeDeletionEvent args)
    {
        ConfirmTrackedDeath(ent);
    }

    private void ConfirmTrackedDeath(Entity<DeathNoteTargetTrackingComponent> ent)
    {
        var now = _timing.CurTime;
        foreach (var (entryId, deadline) in ent.Comp.EntryDeadlines)
        {
            if (deadline < now)
                continue;

            if (_journal.TryUpdateStatus(
                    entryId,
                    DeathNoteEntryStatus.DeathConfirmed,
                    result: "The previously started Death Note effect ended in confirmed target death."))
            {
                _adminLog.Add(
                    LogType.Action,
                    LogImpact.Extreme,
                    $"Death Note entry #{entryId} target death was confirmed after its effect started.");
            }
        }

        // Если к фактической смерти уже не осталось активной необратимой записи,
        // запрет на реанимацию не должен превращать несвязанную смерть в результат Тетради.
        var hasActiveUnrevivableEntry = ent.Comp.UnrevivableEntryIds.Any(entryId =>
            ent.Comp.EntryDeadlines.TryGetValue(entryId, out var deadline) && deadline >= now);
        if (!hasActiveUnrevivableEntry)
            RemoveOwnedUnrevivable(ent.Owner, ent.Comp);

        RemCompDeferred<DeathNoteTargetTrackingComponent>(ent.Owner);
    }

    private void PruneExpiredTargetTracking()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DeathNoteTargetTrackingComponent>();
        while (query.MoveNext(out var target, out var tracker))
        {
            if (tracker.NextExpiry == TimeSpan.Zero || now < tracker.NextExpiry)
                continue;

            List<uint>? expiredEntries = null;
            var nextExpiry = TimeSpan.MaxValue;
            foreach (var (entryId, deadline) in tracker.EntryDeadlines)
            {
                if (deadline <= now)
                {
                    (expiredEntries ??= new List<uint>()).Add(entryId);
                    continue;
                }

                if (deadline < nextExpiry)
                    nextExpiry = deadline;
            }

            if (expiredEntries != null)
            {
                foreach (var entryId in expiredEntries)
                {
                    tracker.EntryDeadlines.Remove(entryId);
                    tracker.UnrevivableEntryIds.Remove(entryId);
                }
            }

            tracker.NextExpiry = nextExpiry == TimeSpan.MaxValue
                ? TimeSpan.Zero
                : nextExpiry;

            if (tracker.UnrevivableEntryIds.Count == 0)
                RemoveOwnedUnrevivable(target, tracker);

            if (tracker.EntryDeadlines.Count == 0)
                RemCompDeferred<DeathNoteTargetTrackingComponent>(target);
        }
    }

    private void RemoveOwnedUnrevivable(
        EntityUid target,
        DeathNoteTargetTrackingComponent tracker)
    {
        if (!tracker.OwnsUnrevivableComponent ||
            !TryComp(target, out UnrevivableComponent? unrevivable) ||
            unrevivable.ReasonMessage != UnrevivableReason)
        {
            tracker.OwnsUnrevivableComponent = false;
            return;
        }

        // Правило раунда отвечает за необратимость уже умерших игроков.
        // Компонент нельзя снять из-за порядка обработки одного события смерти.
        if (_mobState.IsDead(target) && _roundUnrevivableRule.IsActive())
        {
            tracker.OwnsUnrevivableComponent = false;
            return;
        }

        RemComp<UnrevivableComponent>(target);
        tracker.OwnsUnrevivableComponent = false;
    }

    private void DeliverCustomScenario(DeathNoteEntry entry, string scenario, EntityUid target)
    {
        if (_playerManager.TryGetSessionByEntity(target, out var session))
        {
            RaiseNetworkEvent(new DeathNoteInfluenceMessage(scenario, true), session);
            _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.CustomDelivered,
                result: "Private custom scenario was delivered to the target session.");
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"Death Note entry #{entry.EntryId} delivered a private custom scenario to {target:player}.");
            return;
        }

        _journal.TryUpdateStatus(entry.EntryId, DeathNoteEntryStatus.Failed,
            DeathNoteFailureReason.TechnicalError,
            error: "The resolved target had no connected session for scheduled custom delivery.");
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Death Note entry #{entry.EntryId} could not deliver its custom scenario to {target:entity}: no session.");
    }
}
