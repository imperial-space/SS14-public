using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Events;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Выполняет одноразовые события из общего цикла обновления и отменяет их на границе раунда.
/// Изменяемое состояние расписания хранится на отдельной служебной сущности.
/// </summary>
public sealed class DeathNoteSchedulerSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound ||
            !TryGetRuntime(out var runtime) ||
            runtime.Comp.RoundId != _gameTicker.RoundId)
        {
            return;
        }

        var now = _timing.CurTime;
        foreach (var (scheduled, executionTime) in runtime.Comp.ScheduledPhases.ToArray())
        {
            if (executionTime > now || !runtime.Comp.ScheduledPhases.Remove(scheduled))
                continue;

            RaiseLocalEvent(new DeathNoteScheduledEntryEvent(
                scheduled.EntryId,
                scheduled.Phase,
                runtime.Comp.RoundId));
        }
    }

    public bool TrySchedule(uint entryId, DeathNoteScheduledPhase phase, TimeSpan delay)
    {
        var runtime = GetRuntime();
        if (_gameTicker.RunLevel != GameRunLevel.InRound ||
            delay < TimeSpan.Zero ||
            runtime.Comp.RoundId != _gameTicker.RoundId ||
            !runtime.Comp.ScheduledPhases.TryAdd((entryId, phase), _timing.CurTime + delay))
        {
            return false;
        }

        return true;
    }

    private void OnRoundStarted(RoundStartedEvent args)
    {
        var runtime = GetRuntime();
        CancelScheduledEntries(runtime.Comp, raiseEvent: false);
        runtime.Comp.RoundId = args.RoundId;
        runtime.Comp.RoundInitialized = true;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.New == GameRunLevel.PostRound && TryGetRuntime(out var runtime))
            CancelScheduledEntries(runtime.Comp, raiseEvent: true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        if (TryGetRuntime(out var runtime))
            CancelScheduledEntries(runtime.Comp, raiseEvent: false);
    }

    private Entity<DeathNoteSchedulerRuntimeComponent> GetRuntime()
    {
        if (!TryGetRuntime(out var runtime))
        {
            var runtimeUid = Spawn(DeathNoteSchedulerRuntimeComponent.Prototype);
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

    private void CancelScheduledEntries(DeathNoteSchedulerRuntimeComponent runtime, bool raiseEvent)
    {
        if (raiseEvent && runtime.ScheduledPhases.Count > 0)
        {
            var cancelledEntryIds = runtime.ScheduledPhases.Keys
                .Select(scheduled => scheduled.EntryId)
                .Distinct()
                .ToArray();
            RaiseLocalEvent(new DeathNoteTimersCancelledEvent(cancelledEntryIds));
        }

        runtime.ScheduledPhases.Clear();
    }
}
