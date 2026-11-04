using System.Threading;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Helpers.Schedule;

/// <summary>
/// Отложенные действия на игровом времени вместо <c>Timer.Spawn</c>: идут по <see cref="IGameTiming.CurTime"/>
/// (пауза сервера их останавливает), очищаются при рестарте раунда, отменяются вместе с сущностью-владельцем
/// и не роняют сервер, если колбэк бросил исключение.
/// </summary>
public sealed class ImperialScheduleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<ScheduledAction> _queue = new();
    private readonly List<ScheduledAction> _due = new();
    private int _nextId = 1;

    private sealed class ScheduledAction
    {
        public int Id;
        public TimeSpan At;
        public EntityUid? Owner;
        public CancellationToken Token;
        public Action Action = default!;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _queue.Clear();
    }

    /// <summary>Выполнить действие через <paramref name="delay"/>. Если задан владелец и он удалён — действие отменяется.</summary>
    /// <returns>Идентификатор для <see cref="Cancel"/>.</returns>
    public int Schedule(TimeSpan delay, Action action, EntityUid? owner = null)
    {
        var id = _nextId++;
        _queue.Add(new ScheduledAction
        {
            Id = id,
            At = _timing.CurTime + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay),
            Owner = owner,
            Action = action,
        });
        return id;
    }

    /// <summary>То же, но с отменой через <paramref name="token"/> (для мест, где отмена уже построена на CancellationTokenSource).</summary>
    public int Schedule(TimeSpan delay, Action action, CancellationToken token)
    {
        var id = Schedule(delay, action);
        _queue[^1].Token = token;
        return id;
    }

    public void Cancel(int id)
    {
        _queue.RemoveAll(a => a.Id == id);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_queue.Count == 0)
            return;

        var now = _timing.CurTime;
        _due.Clear();
        foreach (var action in _queue)
        {
            if (action.At <= now)
                _due.Add(action);
        }

        if (_due.Count == 0)
            return;

        foreach (var action in _due)
        {
            _queue.Remove(action);
            if (action.Token.IsCancellationRequested || action.Owner is { } owner && TerminatingOrDeleted(owner))
                continue;

            try
            {
                action.Action();
            }
            catch (Exception e)
            {
                Log.Error($"Scheduled action failed: {e}");
            }
        }
    }
}
