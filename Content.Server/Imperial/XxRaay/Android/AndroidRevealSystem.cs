using System;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система, позволяющая RK800 снимать маскировку с других андроидов
/// </summary>
public sealed class AndroidRevealSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidDisguiseComponent, GetVerbsEvent<Verb>>(OnGetRevealVerbs);

        SubscribeLocalEvent<AndroidForcedRevealComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<AndroidForcedRevealComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<AndroidForcedRevealComponent, AndroidRevealDisguiseDoAfterEvent>(OnRevealDoAfter);
        SubscribeLocalEvent<AndroidForcedRevealComponent, AndroidRevealEscapeDoAfterEvent>(OnEscapeDoAfter);
    }

    private void OnGetRevealVerbs(Entity<AndroidDisguiseComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = (EntityUid) ent;
        var user = args.User;

        if (user == target)
            return;

        if (!_mobState.IsAlive(user) || !_mobState.IsAlive(target))
            return;

        if (!TryComp<AndroidStressComponent>(user, out var userStress) || !userStress.CanForceRevealAndroids)
            return;

        if (!TryComp<AndroidDisguiseComponent>(target, out var disguise) ||
            disguise.State != AndroidDisguiseState.Human)
        {
            return;
        }

        if (!TryComp<AndroidDiodeComponent>(target, out var diode) || !diode.HasDiode)
            return;

        if (HasComp<AndroidForcedRevealComponent>(target))
            return;

        var now = _timing.CurTime;
        if (userStress.NextForceRevealTime > now)
            return;

        var verb = new Verb
        {
            Text = Loc.GetString("android-reveal-disguise-verb-name"),
            Message = Loc.GetString("android-reveal-disguise-verb-desc"),
            Act = () => StartReveal(user, target)
        };

        args.Verbs.Add(verb);
    }

    private void StartReveal(EntityUid user, EntityUid target)
    {
        if (!Exists(user) || !Exists(target))
            return;

        if (!_mobState.IsAlive(user) || !_mobState.IsAlive(target))
            return;

        if (!TryComp<AndroidStressComponent>(user, out var userStress) || !userStress.CanForceRevealAndroids)
            return;

        if (!TryComp<AndroidDisguiseComponent>(target, out var disguise) ||
            disguise.State != AndroidDisguiseState.Human)
        {
            return;
        }

        if (!TryComp<AndroidDiodeComponent>(target, out var diode) || !diode.HasDiode)
            return;

        if (HasComp<AndroidForcedRevealComponent>(target))
            return;

        var comp = EnsureComp<AndroidForcedRevealComponent>(target);
        comp.Revealer = user;
        comp.Target = target;
        comp.EscapeInProgress = false;

        _actionBlocker.UpdateCanMove(target);

        _popup.PopupEntity(
            Loc.GetString("android-deviant-consent-convert-popup",
                ("target", Identity.Entity(target, EntityManager))),
            target,
            Filter.Broadcast(),
            true,
            PopupType.LargeCaution);

        var revealDuration = userStress.ForceRevealDuration;
        var maxDistance = userStress.ForceRevealMaxDistance;

        var args = new DoAfterArgs(EntityManager, user, revealDuration,
            new AndroidRevealDisguiseDoAfterEvent(), target, target: target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = maxDistance,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnUpdateCanMove(EntityUid uid, AndroidForcedRevealComponent comp, ref UpdateCanMoveEvent args)
    {
        if (comp.Revealer != null)
            args.Cancel();
    }

    private void OnMoveInput(EntityUid uid, AndroidForcedRevealComponent comp, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (comp.Revealer is not { } revealer || !Exists(revealer))
            return;

        if (comp.EscapeInProgress)
            return;

        if (!TryComp<AndroidStressComponent>(revealer, out var stress))
            return;

        var escapeDuration = stress.ForceRevealEscapeDuration;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, escapeDuration,
            new AndroidRevealEscapeDoAfterEvent(), uid, target: revealer)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        comp.EscapeInProgress = true;
    }

    private void OnRevealDoAfter(EntityUid uid, AndroidForcedRevealComponent comp, ref AndroidRevealDisguiseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            CleanupReveal(uid, ref comp);
            return;
        }

        if (comp.Revealer is not { } revealer || !Exists(revealer))
        {
            CleanupReveal(uid, ref comp);
            return;
        }

        if (!TryComp<AndroidDisguiseComponent>(uid, out var disguise))
        {
            CleanupReveal(uid, ref comp);
            return;
        }

        disguise.State = AndroidDisguiseState.TransformingToAndroid;
        disguise.NextStateTime = _timing.CurTime + disguise.RetransformDuration;
        Dirty(uid, disguise);

        CleanupReveal(uid, ref comp);
        args.Handled = true;
    }

    private void OnEscapeDoAfter(EntityUid uid, AndroidForcedRevealComponent comp, ref AndroidRevealEscapeDoAfterEvent args)
    {
        comp.EscapeInProgress = false;

        if (args.Cancelled || args.Handled)
            return;

        var revealer = comp.Revealer;

        if (revealer is { } r && TryComp<AndroidStressComponent>(r, out var stress))
        {
            var now = _timing.CurTime;
            stress.NextForceRevealTime = now + stress.ForceRevealFailCooldown;
            Dirty(r, stress);
        }

        _popup.PopupEntity(
            Loc.GetString("android-reveal-disguise-escape-success"),
            uid,
            uid,
            PopupType.MediumCaution);

        if (revealer is { } revealerUid && Exists(revealerUid))
        {
            _popup.PopupEntity(
                Loc.GetString("android-reveal-disguise-escape-failed-revealer",
                    ("target", Identity.Entity(uid, EntityManager))),
                revealerUid,
                revealerUid,
                PopupType.SmallCaution);
        }

        CleanupReveal(uid, ref comp);
        args.Handled = true;
    }

    private void CleanupReveal(EntityUid uid, ref AndroidForcedRevealComponent comp)
    {
        comp.Revealer = null;
        comp.Target = null;
        comp.EscapeInProgress = false;

        RemCompDeferred<AndroidForcedRevealComponent>(uid);
        _actionBlocker.UpdateCanMove(uid);
    }
}

