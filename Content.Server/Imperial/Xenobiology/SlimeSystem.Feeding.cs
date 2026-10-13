using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Xenobiology;

public sealed partial class SlimeSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private static readonly SoundSpecifier AttackBlob = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/attackblob.ogg");

    /// <summary>status_effect/slime_leech: тик раз в секунду.</summary>
    private const float LeechInterval = 1f;

    /// <summary>status_effect/slimed: тик раз в 3 секунды.</summary>
    private const float SlimedInterval = 3f;

    private static readonly string[] PainLines =
    {
        "slime-pain-1", "slime-pain-2", "slime-pain-3", "slime-pain-4", "slime-pain-5", "slime-pain-6", "slime-pain-7",
    };

    private void InitializeFeeding()
    {
        SubscribeLocalEvent<SlimeLeechedComponent, MobStateChangedEvent>(OnVictimStateChanged);
        SubscribeLocalEvent<SlimeFoodComponent, MobStateChangedEvent>(OnFoodStateChanged);
        SubscribeLocalEvent<SlimedComponent, Content.Shared.Chemistry.ReactionEntityEvent>(OnSlimedReaction);
    }

    #region Кормление

    /// <summary>can_feed_on(): можно ли высасывать жизнь из цели.</summary>
    public bool CanFeedOn(Entity<SlimeComponent> ent, EntityUid meal, bool silent = true, bool checkAdjacent = false, bool checkFriendship = false)
    {
        string? error = null;
        if (meal == ent.Owner || !HasComp<MobStateComponent>(meal))
            return false;
        if (_mobState.IsIncapacitated(ent))
            error = "slime-feed-unconscious";
        else if (ent.Comp.HungerDisabled)
            error = "slime-feed-not-hungry";
        else if (checkFriendship && TryComp<SlimeBrainComponent>(ent, out var brain) && brain.Friends.Contains(meal))
            return false;
        else if (checkAdjacent && !_transform.InRange(_transform.GetMoverCoordinates(ent), _transform.GetMoverCoordinates(meal), 1.5f))
            return false;
        else if (_mobState.IsDead(meal))
            error = "slime-feed-no-energy";
        else if (HasComp<SlimeLeechedComponent>(meal))
            error = "slime-feed-occupied";
        else if (HasComp<BorgChassisComponent>(meal) || !HasComp<DamageableComponent>(meal))
            error = "slime-feed-no-energy";
        else if (HasComp<SlimeComponent>(meal))
            error = "slime-feed-slime";

        if (error != null)
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString(error), ent, ent);
            return false;
        }

        return true;
    }

    /// <summary>start_feeding(): слайм цепляется к жертве и начинает её высасывать.</summary>
    public void StartFeeding(Entity<SlimeComponent> ent, EntityUid target)
    {
        if (!TryComp<SlimeBrainComponent>(ent, out var brain) || !CanFeedOn(ent, target, silent: false))
            return;

        StopFeeding(ent, brain, silent: true);

        // Слайм садится на жертву: следует за ней и не сталкивается ни с чем.
        _transform.SetCoordinates(ent, new Robust.Shared.Map.EntityCoordinates(target, new Vector2(0f, 0.2f)));
        if (TryComp<PhysicsComponent>(ent, out var physics))
            _physics.SetCanCollide(ent, false, body: physics);

        brain.FeedingOn = target;
        var leech = EnsureComp<SlimeLeechedComponent>(target);
        leech.Slime = ent;
        leech.NextTick = _timing.CurTime + TimeSpan.FromSeconds(LeechInterval);

        _popup.PopupEntity(Loc.GetString("slime-latches", ("slime", ent.Owner), ("target", target)), target, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("slime-feeding-started"), ent, ent);
        UpdateHtn(ent, brain);
    }

    /// <summary>stop_feeding(): слайм отпускает жертву; жертву может покрыть слизь.</summary>
    public void StopFeeding(Entity<SlimeComponent> ent, SlimeBrainComponent brain, bool silent = false)
    {
        if (brain.FeedingOn is not { } victim)
            return;

        brain.FeedingOn = null;
        if (!silent && !TerminatingOrDeleted(victim))
            _popup.PopupEntity(Loc.GetString("slime-lets-go", ("slime", ent.Owner), ("target", victim)), ent);

        if (!TerminatingOrDeleted(ent))
        {
            _transform.AttachToGridOrMap(ent);
            if (TryComp<PhysicsComponent>(ent, out var physics))
                _physics.SetCanCollide(ent, true, body: physics);
        }

        if (!TerminatingOrDeleted(victim) && TryComp<SlimeLeechedComponent>(victim, out var leech) && leech.Slime == ent.Owner)
        {
            RemComp<SlimeLeechedComponent>(victim);
            // on_buckle_end(): без биозащиты жертва покрывается слизью.
            if (_random.Prob(1f - BioProtection(victim)) && _proto.TryIndex(ent.Comp.SlimeType, out var type))
                ApplySlimed(victim, type.RgbCode);
        }

        UpdateHtn(ent, brain);
    }

    /// <summary>Защита от биологических угроз (getarmor BIO): по сопротивлению ядам одежды.</summary>
    private float BioProtection(EntityUid uid)
    {
        var ev = new Content.Shared.Damage.Systems.DamageModifyEvent(new Content.Shared.Damage.DamageSpecifier { DamageDict = { ["Caustic"] = 100 } });
        RaiseLocalEvent(uid, ev);
        var left = ev.Damage.GetTotal().Float();
        return Math.Clamp(1f - left / 100f, 0f, 1f);
    }

    private void UpdateFeeding(Entity<SlimeComponent> ent, SlimeBrainComponent brain, TimeSpan now)
    {
        if (brain.FeedingOn is not { } victim)
            return;

        if (TerminatingOrDeleted(victim) || !TryComp<SlimeLeechedComponent>(victim, out var leech) || leech.Slime != ent.Owner)
        {
            StopFeeding(ent, brain, silent: true);
            return;
        }

        if (now < leech.NextTick)
            return;
        leech.NextTick = now + TimeSpan.FromSeconds(LeechInterval);

        if (_mobState.IsIncapacitated(ent))
        {
            StopFeeding(ent, brain, silent: true);
            return;
        }

        if (_mobState.IsDead(victim))
        {
            OnVictimDrained(ent, brain, victim);
            return;
        }

        // tick(): 1–2 грубого и 0.5–1 токсинов в секунду.
        var brute = _random.Next(2, 5) * 0.5f * LeechInterval;
        var tox = _random.Next(1, 3) * 0.5f * LeechInterval;
        var spec = new Content.Shared.Damage.DamageSpecifier();
        spec.DamageDict["Blunt"] = brute;
        spec.DamageDict["Poison"] = tox;
        if (!_damageable.TryChangeDamage(victim, spec, out var dealt, origin: ent))
        {
            _popup.PopupEntity(Loc.GetString("slime-feed-not-food"), ent, ent);
            StopFeeding(ent, brain);
            return;
        }

        var total = dealt.GetTotal().Float();
        if (total > 0f && _random.Prob(1f - MathF.Pow(0.95f, LeechInterval)) && HasComp<ActorComponent>(victim))
            _popup.PopupEntity(Loc.GetString(_random.Pick(PainLines)), victim, victim, PopupType.LargeCaution);

        AdjustNutrition(ent, 1.8f * total);
        Damage(ent, "Blunt", -1.5f * LeechInterval);
    }

    /// <summary>Жертва умерла: возможное бешенство после игрока и дружба по феромонам.</summary>
    private void OnVictimDrained(Entity<SlimeComponent> ent, SlimeBrainComponent brain, EntityUid victim)
    {
        if (HasComp<ActorComponent>(ent))
            _popup.PopupEntity(Loc.GetString("slime-victim-drained"), ent, ent);

        // COMSIG_SLIME_DRAINED → status_effect/slime_food
        if (TryComp<SlimeFoodComponent>(victim, out var food))
        {
            if (food.Feeder is { } feeder && !TerminatingOrDeleted(feeder) && !ent.Comp.Rabid && _random.Prob(food.BefriendChance / 100f))
            {
                Befriend(ent, feeder);
                Spawn(HeartEffect, _transform.GetMoverCoordinates(ent));
            }

            RemComp<SlimeFoodComponent>(victim);
        }

        if (_random.Prob(0.6f) && HasComp<HumanoidProfileComponent>(victim) && HasComp<ActorComponent>(victim) && !ent.Comp.Rabid)
            SetRabid(ent, true);

        StopFeeding(ent, brain);
    }

    private void OnVictimStateChanged(Entity<SlimeLeechedComponent> ent, ref MobStateChangedEvent args)
    {
        // Обработка смерти — в тике кормления (как в tick() статуса).
    }

    private void OnFoodStateChanged(Entity<SlimeFoodComponent> ent, ref MobStateChangedEvent args)
    {
    }

    #endregion

    #region Слизь

    /// <summary>wet_stacks: вода смывает слизь (5 единиц воды — 1 стак).</summary>
    private void OnSlimedReaction(Entity<SlimedComponent> ent, ref Content.Shared.Chemistry.ReactionEntityEvent args)
    {
        if (args.Method != Content.Shared.Chemistry.ReactionMethod.Touch || args.Reagent.ID != "Water")
            return;
        RemoveSlimedStacks(ent, args.ReagentQuantity.Quantity.Float() / 5f);
    }

    /// <summary>status_effect/slimed: слизь разъедает жертву, пока её не смоют.</summary>
    public void ApplySlimed(EntityUid victim, Color color)
    {
        if (OrganicHealth(victim) <= 10f)
            return;

        var slimed = EnsureComp<SlimedComponent>(victim);
        slimed.Stacks = 10;
        slimed.Color = color;
        slimed.NextTick = _timing.CurTime + TimeSpan.FromSeconds(SlimedInterval);
        Dirty(victim, slimed);
        _popup.PopupEntity(Loc.GetString("slimed-applied"), victim, victim, PopupType.LargeCaution);
    }

    /// <summary>get_organic_health(): здоровье до крита.</summary>
    private float OrganicHealth(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 0f;
        if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit) && !_thresholds.TryGetDeadThreshold(uid, out crit))
            return 100f;
        return (crit!.Value - _damageable.GetTotalDamage((uid, damageable))).Float();
    }

    /// <summary>remove_stacks(): смыть часть слизи.</summary>
    public void RemoveSlimedStacks(Entity<SlimedComponent> ent, float stacks = 1f)
    {
        ent.Comp.Stacks -= stacks;
        if (ent.Comp.Stacks <= 0)
        {
            _popup.PopupEntity(Loc.GetString("slimed-washed"), ent, ent);
            RemComp<SlimedComponent>(ent);
            return;
        }

        if (_random.Prob(0.1f))
            _popup.PopupEntity(Loc.GetString("slimed-thinner"), ent, ent);
        Dirty(ent);
    }

    private void UpdateSlimed(TimeSpan now)
    {
        var query = EntityQueryEnumerator<SlimedComponent>();
        var toWash = new List<Entity<SlimedComponent>>();
        var toRemove = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var slimed))
        {
            if (now < slimed.NextTick)
                continue;
            slimed.NextTick = now + TimeSpan.FromSeconds(SlimedInterval);

            if (OrganicHealth(uid) <= 10f)
            {
                _popup.PopupEntity(Loc.GetString("slimed-crawls-off"), uid, uid);
                toRemove.Add(uid);
                continue;
            }

            Damage(uid, "Blunt", _random.Next(2, 5) * SlimedInterval);
            if (_random.Prob(1f - MathF.Pow(0.9f, SlimedInterval)))
                _popup.PopupEntity(Loc.GetString(_random.Pick(new[] { "slimed-pain-1", "slimed-pain-2", "slimed-pain-3" })), uid, uid, PopupType.LargeCaution);
        }

        foreach (var uid in toRemove)
        {
            RemComp<SlimedComponent>(uid);
        }

        foreach (var slimed in toWash)
        {
            RemoveSlimedStacks(slimed, SlimedInterval);
        }
    }

    #endregion
}
