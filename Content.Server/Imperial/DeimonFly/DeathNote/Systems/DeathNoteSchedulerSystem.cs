using System.Linq;
using System.Threading;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Shared.GameTicking;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Планирует одноразовые события без общего цикла обновления и отменяет их на границе раунда.
/// Изменяемое состояние расписания хранится на отдельной служебной сущности.
/// </summary>
public sealed class DeathNoteSchedulerSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DeathNoteSchedulerRuntimeComponent, ComponentShutdown>(OnRuntimeShutdown);
    }

    public override void Shutdown()
    {
        if (TryGetRuntime(out var runtime))
            DisposeRuntime(runtime);

        base.Shutdown();
    }

    public bool TrySchedule(uint entryId, DeathNoteScheduledPhase phase, TimeSpan delay)
    {
        var runtime = GetRuntime();
        if (_gameTicker.RunLevel != GameRunLevel.InRound ||
            delay < TimeSpan.Zero ||
            runtime.Comp.RoundId != _gameTicker.RoundId ||
            runtime.Comp.CancellationDisposed ||
            runtime.Comp.RoundCancellation.IsCancellationRequested ||
            !runtime.Comp.ScheduledPhases.Add((entryId, phase)))
        {
            return false;
        }

        var runtimeUid = runtime.Owner;
        var capturedRoundId = runtime.Comp.RoundId;
        var token = runtime.Comp.RoundCancellation.Token;
        Timer.Spawn(
            delay,
            () => OnTimer(runtimeUid, entryId, phase, capturedRoundId, token),
            token);
        return true;
    }

    private void OnTimer(
        EntityUid runtimeUid,
        uint entryId,
        DeathNoteScheduledPhase phase,
        int capturedRoundId,
        CancellationToken token)
    {
        if (token.IsCancellationRequested ||
            !TryComp(runtimeUid, out DeathNoteSchedulerRuntimeComponent? runtime))
        {
            return;
        }

        runtime.ScheduledPhases.Remove((entryId, phase));
        if (_gameTicker.RunLevel != GameRunLevel.InRound ||
            capturedRoundId != runtime.RoundId ||
            capturedRoundId != _gameTicker.RoundId)
        {
            return;
        }

        RaiseLocalEvent(new DeathNoteScheduledEntryEvent(entryId, phase, capturedRoundId));
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        var runtime = GetRuntime();
        ResetCancellation(runtime.Comp);
        runtime.Comp.RoundId = args.RoundId;
        runtime.Comp.RoundInitialized = true;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New == GameRunLevel.PostRound && TryGetRuntime(out var runtime))
            CancelTimers(runtime.Comp, raiseEvent: true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        if (TryGetRuntime(out var runtime))
            CancelTimers(runtime.Comp, raiseEvent: false);
    }

    private void OnRuntimeShutdown(
        Entity<DeathNoteSchedulerRuntimeComponent> ent,
        ref ComponentShutdown args)
    {
        DisposeRuntime(ent);
    }

    private Entity<DeathNoteSchedulerRuntimeComponent> GetRuntime()
    {
        if (!TryGetRuntime(out var runtime))
        {
            var runtimeUid = Spawn(DeathNoteRuntimePrototypes.RoundState);
            runtime = (runtimeUid, Comp<DeathNoteSchedulerRuntimeComponent>(runtimeUid));
        }

        if (!runtime.Comp.RoundInitialized)
        {
            runtime.Comp.RoundId = _gameTicker.RoundId;
            runtime.Comp.RoundInitialized = true;
        }

        return runtime;
    }

    private bool TryGetRuntime(out Entity<DeathNoteSchedulerRuntimeComponent> runtime)
    {
        var query = EntityQueryEnumerator<DeathNoteSchedulerRuntimeComponent>();
        if (query.MoveNext(out var uid, out var component))
        {
            runtime = (uid, component);
            return true;
        }

        runtime = default;
        return false;
    }

    private void DisposeRuntime(Entity<DeathNoteSchedulerRuntimeComponent> runtime)
    {
        if (runtime.Comp.CancellationDisposed)
            return;

        CancelTimers(runtime.Comp, raiseEvent: false);
        runtime.Comp.RoundCancellation.Dispose();
        runtime.Comp.CancellationDisposed = true;
    }

    private void ResetCancellation(DeathNoteSchedulerRuntimeComponent runtime)
    {
        CancelTimers(runtime, raiseEvent: false);
        if (!runtime.CancellationDisposed)
            runtime.RoundCancellation.Dispose();

        runtime.RoundCancellation = new CancellationTokenSource();
        runtime.CancellationDisposed = false;
    }

    private void CancelTimers(DeathNoteSchedulerRuntimeComponent runtime, bool raiseEvent)
    {
        if (raiseEvent && runtime.ScheduledPhases.Count > 0)
        {
            var cancelledEntryIds = runtime.ScheduledPhases
                .Select(scheduled => scheduled.EntryId)
                .Distinct()
                .ToArray();
            RaiseLocalEvent(new DeathNoteTimersCancelledEvent(cancelledEntryIds));
        }

        if (!runtime.CancellationDisposed)
            runtime.RoundCancellation.Cancel();

        runtime.ScheduledPhases.Clear();
    }
}
