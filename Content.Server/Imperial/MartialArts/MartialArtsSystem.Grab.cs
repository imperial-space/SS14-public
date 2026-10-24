using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Стадии захвата TG (GRAB_PASSIVE → AGGRESSIVE → NECK → KILL) поверх тяги SS14.
/// Усиливают захват только мастера боевых искусств: повторная тяга в боевом режиме — это «G».
/// </summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    /// <summary>BASE_GRAB_RESIST_CHANCE.</summary>
    private const float BaseGrabResistChance = 60f;

    /// <summary>grab_upgrade_time = 30 (деци-секунд).</summary>
    private static readonly TimeSpan GrabUpgradeTime = TimeSpan.FromSeconds(3);

    /// <summary>client.move_delay = world.time + 4 SECONDS после неудачного рывка.</summary>
    private static readonly TimeSpan ResistDelay = TimeSpan.FromSeconds(4);

    /// <summary>Удушение: один тик дыхания SS13.</summary>
    private static readonly TimeSpan StrangleInterval = TimeSpan.FromSeconds(2);

    private static readonly SoundPathSpecifier GrabSound = new("/Audio/Effects/thudswoosh.ogg");

    /// <summary>Какую стадию поставить, когда тяга начнётся (приём уже решил за нас).</summary>
    private readonly Dictionary<EntityUid, MartialGrabState> _pendingGrabState = new();

    private void InitializeGrab()
    {
        SubscribeLocalEvent<MartialArtsComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<MartialArtsComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<MartialArtsComponent, PullStoppedMessage>(OnPullStopped);
        SubscribeLocalEvent<MartialArtsComponent, MartialGrabUpgradeDoAfterEvent>(OnGrabUpgradeDoAfter);
        SubscribeLocalEvent<AttemptStopPullingEvent>(OnAttemptStopPulling);
    }

    /// <summary>attempt_grab() → grab_act(): первая тяга — это «G».</summary>
    private void OnPullAttempt(Entity<MartialArtsComponent> ent, ref PullAttemptEvent args)
    {
        if (args.Cancelled || args.PullerUid != ent.Owner || _skipGrabAct.Contains(ent.Owner))
            return;

        var target = args.PulledUid;
        if (!IsValidTarget(ent, target) || GetActive(ent.Comp) is not { } art || !CanUse(ent, art))
            return;

        switch (GrabAct(ent, art, target, upgrade: false))
        {
            case MartialResult.Fail:
            case MartialResult.Success:
                args.Cancelled = true;
                break;
        }
    }

    private void OnPullStarted(Entity<MartialArtsComponent> ent, ref PullStartedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        ent.Comp.GrabTarget = args.PulledUid;
        ent.Comp.GrabState = _pendingGrabState.Remove(ent.Owner, out var state) ? state : MartialGrabState.Passive;
        EnsureComp<MartialGrabbedComponent>(args.PulledUid).Grabber = ent.Owner;
        ent.Comp.NextStrangleTick = _timing.CurTime + StrangleInterval;
    }

    private void OnPullStopped(Entity<MartialArtsComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        StopGrab(ent, ent.Comp);
    }

    private void StopGrab(EntityUid user, MartialArtsComponent comp)
    {
        if (comp.GrabTarget is { } target && TryComp<MartialGrabbedComponent>(target, out var grabbed) && grabbed.Grabber == user)
        {
            RemComp<MartialGrabbedComponent>(target);
            if (comp.GrabState == MartialGrabState.Kill)
                _statusEffects.TryRemoveStatusEffect(target, "Muted");
        }

        comp.GrabTarget = null;
        comp.GrabState = MartialGrabState.Passive;
        comp.UpgradingGrab = false;
    }

    /// <summary>
    /// Боец просит отпустить тягу в боевом режиме — это повторный захват («G», grippedby).
    /// Пленник просит отпустить — это resist_grab().
    /// </summary>
    private void OnAttemptStopPulling(ref AttemptStopPullingEvent args)
    {
        if (args.Cancelled || args.User is not { } user)
            return;

        if (TryComp<MartialArtsComponent>(user, out var comp) && comp.GrabTarget is { } target)
        {
            // На удушении дальше усиливать нечего — тогда тягу можно просто отпустить.
            if (!_combat.IsInCombatMode(user) || comp.GrabState >= MartialGrabState.Kill)
                return;

            args.Cancelled = true;
            HandleGrabAgain((user, comp), target);
            return;
        }

        if (TryComp<MartialGrabbedComponent>(user, out var grabbed) && !ResistGrab(user, grabbed))
            args.Cancelled = true;
    }

    /// <summary>Повторный «G» по тому, кого уже держим.</summary>
    private void HandleGrabAgain(Entity<MartialArtsComponent> ent, EntityUid target)
    {
        if (GetActive(ent.Comp) is { } art && CanUse(ent, art))
        {
            var result = GrabAct(ent, art, target, upgrade: true);
            if (result != MartialResult.Invalid)
                return;
        }

        UpgradeGrab(ent, target, instant: false);
    }

    /// <summary>grippedby(): первая ступень мгновенно, следующие — за 3 секунды.</summary>
    public void UpgradeGrab(Entity<MartialArtsComponent> ent, EntityUid target, bool instant)
    {
        if (ent.Comp.GrabTarget != target || ent.Comp.GrabState >= MartialGrabState.Kill || ent.Comp.UpgradingGrab)
            return;

        _audio.PlayPvs(GrabSound, target);

        if (ent.Comp.GrabState == MartialGrabState.Passive || instant)
        {
            SetGrabState(ent, target, ent.Comp.GrabState + 1);
            return;
        }

        ent.Comp.UpgradingGrab = true;
        PopupPair(ent, target, "martial-grab-upgrading-others", "martial-grab-upgrading-self", "martial-grab-upgrading-target");

        var doAfter = new DoAfterArgs(EntityManager, ent, GrabUpgradeTime, new MartialGrabUpgradeDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            ent.Comp.UpgradingGrab = false;
    }

    private void OnGrabUpgradeDoAfter(Entity<MartialArtsComponent> ent, ref MartialGrabUpgradeDoAfterEvent args)
    {
        ent.Comp.UpgradingGrab = false;
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.GrabTarget != target)
            return;

        args.Handled = true;
        SetGrabState(ent, target, ent.Comp.GrabState + 1);
    }

    /// <summary>setGrabState() с сообщениями grippedby().</summary>
    public void SetGrabState(Entity<MartialArtsComponent> ent, EntityUid target, MartialGrabState state)
    {
        var old = ent.Comp.GrabState;
        ent.Comp.GrabState = state;

        switch (state)
        {
            case MartialGrabState.Aggressive:
                PopupPair(ent, target, "martial-grab-aggressive-others", "martial-grab-aggressive-self", "martial-grab-aggressive-target");
                // stop_pulling(): схваченный бросает того, кого тащил сам.
                if (TryComp<PullerComponent>(target, out var puller) && puller.Pulling is { } pulled && TryComp<PullableComponent>(pulled, out var pullable))
                    _pulling.TryStopPull(pulled, pullable);
                break;
            case MartialGrabState.Neck:
                PopupPair(ent, target, "martial-grab-neck-others", "martial-grab-neck-self", "martial-grab-neck-target");
                break;
            case MartialGrabState.Kill:
                PopupPair(ent, target, "martial-grab-kill-others", "martial-grab-kill-self", "martial-grab-kill-target");
                ent.Comp.NextStrangleTick = _timing.CurTime + StrangleInterval;
                break;
        }

        if (old == MartialGrabState.Kill && state != MartialGrabState.Kill)
            _statusEffects.TryRemoveStatusEffect(target, "Muted");
    }

    /// <summary>resist_grab(): шанс вырваться делится на стадию захвата.</summary>
    private bool ResistGrab(EntityUid victim, MartialGrabbedComponent grabbed)
    {
        if (!TryComp<MartialArtsComponent>(grabbed.Grabber, out var grabber) || grabber.GrabTarget != victim)
            return true;

        var now = _timing.CurTime;
        if (now < grabber.NextResist)
            return false;

        var effective = (int) grabber.GrabState;
        var escape = BaseGrabResistChance;
        var failDamage = _random.Next(7, 14);

        if (_standing.IsDown(victim))
            effective++;

        // TRAIT_GRABWEAKNESS (причуда «Слабак на захват» из Imperial/Quirks): захват считается на стадию сильнее.
        if (EntityManager.ComponentFactory.TryGetRegistration("ImperialQuirkPushover", out var pushover) && HasComp(victim, pushover.Type))
            effective++;

        if (GetActive(grabber) is { } art && CanUse(grabbed.Grabber, art))
        {
            effective += art.GrabStateModifier;
            failDamage += (int) art.GrabDamageModifier;
            escape += art.GrabEscapeChanceModifier;
        }

        // На пассивном захвате вырваться можно всегда.
        if (effective < (int) MartialGrabState.Aggressive)
            return true;

        var chance = Math.Clamp(escape / effective, 0f, 100f);
        if (_random.Prob(chance / 100f))
        {
            PopupPair(victim, grabbed.Grabber, "martial-grab-broke-others", "martial-grab-broke-self", "martial-grab-broke-grabber");
            return true;
        }

        _stamina.TakeStaminaDamage(victim, failDamage);
        PopupPair(victim, grabbed.Grabber, "martial-grab-resist-fail-others", "martial-grab-resist-fail-self", "martial-grab-resist-fail-grabber");
        grabber.NextResist = now + ResistDelay;
        return false;
    }

    /// <summary>GRAB_KILL: удушение не даёт дышать и говорить.</summary>
    private void UpdateGrab(Entity<MartialArtsComponent> ent, TimeSpan now)
    {
        if (ent.Comp.GrabTarget is not { } target || ent.Comp.GrabState != MartialGrabState.Kill)
            return;

        if (!Exists(target) || _mobs.IsDead(target))
            return;

        if (now < ent.Comp.NextStrangleTick)
            return;

        ent.Comp.NextStrangleTick = now + StrangleInterval;
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", StrangleInterval * 2, true);

        var oxy = new DamageSpecifier();
        oxy.DamageDict["Asphyxiation"] = FixedPoint2.New(3);
        _damage.TryChangeDamage(target, oxy, ignoreResistances: true, origin: ent.Owner);
    }

    /// <summary>Тянуть цель своей рукой: start_pulling() для приёмов, которые сами берут в захват.</summary>
    public bool StartGrab(EntityUid user, EntityUid target, MartialGrabState state)
    {
        if (!TryComp<PullableComponent>(target, out var pullable) || !TryComp<PullerComponent>(user, out var puller))
            return false;

        if (pullable.Puller == user)
        {
            if (TryComp<MartialArtsComponent>(user, out var comp) && comp.GrabState < state)
                SetGrabState((user, comp), target, state);
            return true;
        }

        _pendingGrabState[user] = state;
        _skipGrabAct.Add(user);
        var started = _pulling.TryStartPull(user, target, puller, pullable);
        _skipGrabAct.Remove(user);

        if (!started)
            _pendingGrabState.Remove(user);

        return started;
    }

    /// <summary>Тяга, которую начал сам приём, не должна снова считаться «G».</summary>
    private readonly HashSet<EntityUid> _skipGrabAct = new();

    /// <summary>Пары сообщений visible_message: окружающим, себе и цели.</summary>
    public void PopupPair(EntityUid user, EntityUid target, string othersKey, string selfKey, string? targetKey = null, PopupType type = PopupType.MediumCaution)
    {
        var args = new (string, object)[] { ("user", Identity(user)), ("target", Identity(target)) };
        var others = Filter.PvsExcept(user).RemoveWhereAttachedEntity(e => e == target);
        _popup.PopupEntity(Loc.GetString(othersKey, args), target, others, true, type);
        _popup.PopupEntity(Loc.GetString(selfKey, args), user, user, type);
        if (targetKey != null)
            _popup.PopupEntity(Loc.GetString(targetKey, args), target, target, PopupType.LargeCaution);
    }

    private EntityUid Identity(EntityUid uid) => Content.Shared.IdentityManagement.Identity.Entity(uid, EntityManager);
}
