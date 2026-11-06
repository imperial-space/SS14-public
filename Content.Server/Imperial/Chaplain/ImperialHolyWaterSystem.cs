using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Святая вода в теле (reagent/water/holywater из SS13). Пока она действует, существо считается святым
/// (защищено от нечестивой магии), дрожит, а дольше 25 секунд — заикается и пошатывается.
/// Что она делает с последователями тёмных сил, решают их системы по событиям
/// <see cref="ImperialHolyWaterStartedEvent"/>, <see cref="ImperialHolyWaterTickEvent"/> и <see cref="ImperialHolyWaterPurgedEvent"/>.
/// </summary>
public sealed class ImperialHolyWaterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;

    private const string HolyWater = "Holywater";
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    /// <summary>~10 единиц в SS13.</summary>
    private const float StutterThreshold = 25f;

    /// <summary>~24 единицы в SS13.</summary>
    private const float PurgeThreshold = 60f;

    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        var seconds = (float)TickInterval.TotalSeconds;

        var query = EntityQueryEnumerator<BloodstreamComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (GetHolyWater(uid) <= FixedPoint2.Zero)
            {
                RemCompDeferred<ImperialHolyWaterComponent>(uid);
                continue;
            }

            var isNew = !HasComp<ImperialHolyWaterComponent>(uid);
            var holy = EnsureComp<ImperialHolyWaterComponent>(uid);
            if (isNew)
            {
                var started = new ImperialHolyWaterStartedEvent();
                RaiseLocalEvent(uid, ref started);
            }

            holy.Seconds += TimeSpan.FromSeconds(seconds);
            Tick(uid, holy, now);
        }
    }

    private void Tick(EntityUid uid, ImperialHolyWaterComponent holy, TimeSpan now)
    {
        _jitter.DoJitter(uid, holy.TickTime, true, 10f, 4f);

        var stuttering = holy.Seconds.TotalSeconds >= StutterThreshold;
        if (stuttering)
        {
            _stutter.DoStutter(uid, holy.TickTime, true);
            _drunk.TryApplyDrunkenness(uid, holy.TickBoozePowerDelay);
        }

        var tick = new ImperialHolyWaterTickEvent((uid, holy), now, stuttering);
        RaiseLocalEvent(uid, ref tick);

        if (holy.Seconds.TotalSeconds < PurgeThreshold)
            return;

        // Минута святой воды: тёмные силы изгнаны, остаток воды выходит из тела.
        var purged = new ImperialHolyWaterPurgedEvent((uid, holy));
        RaiseLocalEvent(uid, ref purged);

        RemoveHolyWater(uid);
        RemCompDeferred<ImperialHolyWaterComponent>(uid);
    }

    private FixedPoint2 GetHolyWater(EntityUid uid)
    {
        var total = FixedPoint2.Zero;
        foreach (var solution in EnumerateBodySolutions(uid))
        {
            var contents = solution.Comp.Solution;
            total += contents.GetTotalPrototypeQuantity(HolyWater);
        }

        return total;
    }

    private void RemoveHolyWater(EntityUid uid)
    {
        foreach (var solution in EnumerateBodySolutions(uid))
        {
            var contents = solution.Comp.Solution;
            var amount = contents.GetTotalPrototypeQuantity(HolyWater);
            if (amount > FixedPoint2.Zero)
                _solution.RemoveReagent(solution, HolyWater, amount);
        }
    }

    /// <summary>Растворы тела (кровь, метаболиты) и желудков.</summary>
    private IEnumerable<Entity<SolutionComponent>> EnumerateBodySolutions(EntityUid uid)
    {
        if (TryComp<SolutionContainerManagerComponent>(uid, out var manager))
        {
            foreach (var (_, solution) in _solution.EnumerateSolutions((uid, manager)))
            {
                yield return solution;
            }
        }

        // У мобов без тела (ксеноморфы, простые мобы) органов нет — без проверки BodySystem пишет ошибку каждый тик.
        if (!HasComp<BodyComponent>(uid) || !_body.TryGetOrgansWithComponent<StomachComponent>(uid, out var stomachs))
            yield break;

        foreach (var stomach in stomachs)
        {
            if (!TryComp<SolutionContainerManagerComponent>(stomach, out var stomachManager))
                continue;

            foreach (var (_, solution) in _solution.EnumerateSolutions((stomach.Owner, stomachManager)))
            {
                yield return solution;
            }
        }
    }
}
