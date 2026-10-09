using Content.Server.Chat.Systems;
using Content.Server.Imperial.Cult;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Drunk;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Святая вода в теле (reagent/water/holywater из SS13). Пока она действует, существо считается святым
/// (защищено от нечестивой магии), дрожит, а дольше 25 секунд — заикается и пошатывается.
/// Культисту она стирает подготовленные заклинания крови, вызывает выкрики и припадки, а через минуту
/// очищает от власти Нар'Си. Еретику не даёт колдовать (см. <see cref="ImperialMagicCastAttemptEvent"/>).
/// </summary>
public sealed class ImperialHolyWaterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CultSystem _cult = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    private const string HolyWater = "Holywater";
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SpellClearCooldown = TimeSpan.FromSeconds(3);

    /// <summary>~10 единиц в SS13.</summary>
    private const float StutterThreshold = 25f;

    /// <summary>~24 единицы в SS13.</summary>
    private const float PurgeThreshold = 60f;

    private static readonly string[] CultPhrases =
    {
        "Av'te Nar'Sie", "Pa'lid Mors", "INO INO ORA ANA", "SAT ANA!",
        "Daim'niodeis Arc'iai Le'eones", "R'ge Na'sie", "Diabo us Vo'iscum", "Eld' Mon Nobis",
    };

    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;
        var seconds = (float) TickInterval.TotalSeconds;

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
            if (isNew && HasComp<CultistComponent>(uid))
                _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-start"), uid, uid, PopupType.LargeCaution);

            holy.Seconds += seconds;
            Tick(uid, holy, now);
        }
    }

    private void Tick(EntityUid uid, ImperialHolyWaterComponent holy, TimeSpan now)
    {
        var cultist = HasComp<CultistComponent>(uid);
        _jitter.DoJitter(uid, TimeSpan.FromSeconds(2), true, 10f, 4f);

        if (now >= holy.NextSpellClear)
        {
            if (cultist && _cult.ClearPreparedSpells(uid))
            {
                _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-spells"), uid, uid, PopupType.LargeCaution);
                holy.NextSpellClear = now + SpellClearCooldown;
            }
        }

        if (holy.Seconds >= StutterThreshold)
        {
            _stutter.DoStutter(uid, TimeSpan.FromSeconds(2), true);
            _drunk.TryApplyDrunkenness(uid, TimeSpan.FromSeconds(2));

            if (cultist && _random.Prob(0.1f))
            {
                _chat.TrySendInGameICMessage(uid, _random.Pick(CultPhrases), InGameICChatType.Speak, false, ignoreActionBlocker: true);
                if (_random.Prob(0.1f))
                {
                    _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-seizure", ("target", uid)), uid, PopupType.LargeCaution);
                    _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(12));
                }
            }
        }

        if (holy.Seconds < PurgeThreshold)
            return;

        // Минута святой воды: культист очищен, остаток воды выходит из тела.
        if (cultist)
        {
            _cult.RemoveCultist(uid);
            _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(10));
            _popup.PopupEntity(Loc.GetString("imperial-holy-water-cult-purged"), uid, uid, PopupType.LargeCaution);
        }

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
