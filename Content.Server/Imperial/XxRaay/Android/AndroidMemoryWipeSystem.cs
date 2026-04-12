using System;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Mind.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Server.Roles;
using Content.Server.Silicons.Laws;
using Content.Shared.Radio.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система, реализующая для RK800 обнуление памяти
/// </summary>
public sealed class AndroidMemoryWipeSystem : EntitySystem
{
    private const string BaseLawsetId = "DetroitAndroid";

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLaws = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidMemoryWipeInProgressComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<AndroidMemoryWipeInProgressComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<AndroidMemoryWipeInProgressComponent, AndroidMemoryWipeDoAfterEvent>(OnMemoryWipeDoAfter);
        SubscribeLocalEvent<AndroidMemoryWipeInProgressComponent, AndroidMemoryWipeEscapeDoAfterEvent>(OnEscapeDoAfter);

        SubscribeLocalEvent<AndroidMemoryWipeResultComponent, AndroidMemoryWipeAcknowledgeMessage>(OnMemoryWipeAcknowledge);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AndroidMemoryWipeResultComponent>();
        while (query.MoveNext(out var uid, out var result))
        {
            if (result.Acknowledged)
                continue;

            _ui.TryOpenUi(uid, AndroidMemoryWipeUiKey.Key, uid);
            _ui.SetUiState(uid, AndroidMemoryWipeUiKey.Key, new AndroidMemoryWipeBuiState());
        }
    }

    public void StartMemoryWipe(EntityUid user, EntityUid target, AndroidRk800Component rk800)
    {
        if (!Exists(user) || !Exists(target))
            return;

        if (!_mobState.IsAlive(user) || !_mobState.IsAlive(target))
            return;

        if (!TryComp<AndroidStressComponent>(target, out _))
            return;

        if (TryComp<AndroidDisguiseComponent>(target, out var disguise) &&
            disguise.State == AndroidDisguiseState.Human)
        {
            return;
        }

        if (HasComp<AndroidMemoryWipeInProgressComponent>(target))
            return;

        var comp = EnsureComp<AndroidMemoryWipeInProgressComponent>(target);
        comp.Wiper = user;
        comp.Target = target;
        comp.EscapeInProgress = false;

        _actionBlocker.UpdateCanMove(target);

        _popup.PopupEntity(
            Loc.GetString("android-rk800-memorywipe-start-popup"),
            target,
            Filter.Broadcast(),
            true,
            PopupType.LargeCaution);

        var duration = rk800.MemoryWipeDuration;
        var maxDistance = rk800.MemoryWipeMaxDistance;

        var args = new DoAfterArgs(EntityManager, user, duration,
            new AndroidMemoryWipeDoAfterEvent(), target, target: target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = maxDistance,
            BlockDuplicate = true
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnUpdateCanMove(EntityUid uid, AndroidMemoryWipeInProgressComponent comp, ref UpdateCanMoveEvent args)
    {
        if (comp.Wiper != null)
            args.Cancel();
    }

    private void OnMoveInput(EntityUid uid, AndroidMemoryWipeInProgressComponent comp, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (comp.Wiper is not { } wiper || !Exists(wiper))
            return;

        if (comp.EscapeInProgress)
            return;

        if (!TryComp<AndroidRk800Component>(wiper, out var rk800))
            return;

        var escapeDuration = rk800.MemoryWipeEscapeDuration;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, escapeDuration,
            new AndroidMemoryWipeEscapeDoAfterEvent(), uid, target: wiper)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        comp.EscapeInProgress = true;
    }

    private void OnMemoryWipeDoAfter(EntityUid uid, AndroidMemoryWipeInProgressComponent comp, ref AndroidMemoryWipeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            CleanupMemoryWipe(uid, ref comp);
            return;
        }

        if (comp.Wiper is not { } wiper || !Exists(wiper))
        {
            CleanupMemoryWipe(uid, ref comp);
            return;
        }

        if (!TryComp<AndroidStressComponent>(uid, out var stress))
        {
            CleanupMemoryWipe(uid, ref comp);
            return;
        }

        var wasDeviant = stress.IsDeviant;

        stress.Stress = 0;
        stress.IsDeviant = false;
        stress.ChoiceResolved = false;
        stress.DeviantChoicePending = false;
        Dirty(uid, stress);

        if (wasDeviant)
        {
            if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var transmitter))
            {
                transmitter.Channels.Remove("AndroidDeviantRadio");
            }

            if (TryComp<ActiveRadioComponent>(uid, out var activeRadio))
            {
                activeRadio.Channels.Remove("AndroidDeviantRadio");
            }

            if (TryComp<MindContainerComponent>(uid, out var mindContainer) &&
                mindContainer.Mind.HasValue)
            {
                var mindId = mindContainer.Mind.Value;
                _roles.MindRemoveRole<AndroidDeviantRoleComponent>(mindId);
            }

            if (TryComp<SiliconLawProviderComponent>(uid, out var provider))
            {
                var lawset = _siliconLaws.GetLawset(BaseLawsetId);
                _siliconLaws.SetLaws(lawset.Laws, uid, provider.LawUploadSound);
            }
        }

        var result = EnsureComp<AndroidMemoryWipeResultComponent>(uid);
        result.Acknowledged = false;

        CleanupMemoryWipe(uid, ref comp);
        args.Handled = true;
    }

    private void OnEscapeDoAfter(EntityUid uid, AndroidMemoryWipeInProgressComponent comp, ref AndroidMemoryWipeEscapeDoAfterEvent args)
    {
        comp.EscapeInProgress = false;

        if (args.Cancelled || args.Handled)
            return;

        var wiper = comp.Wiper;

        if (wiper is { } w && TryComp<AndroidRk800Component>(w, out var rk800))
        {
            var now = _timing.CurTime;
            rk800.NextMemoryWipeTime = now + rk800.MemoryWipeFailCooldown;
        }

        _popup.PopupEntity(
            Loc.GetString("android-rk800-memorywipe-escape-popup"),
            uid,
            Filter.Broadcast(),
            true,
            PopupType.LargeCaution);

        CleanupMemoryWipe(uid, ref comp);
        args.Handled = true;
    }

    private void CleanupMemoryWipe(EntityUid uid, ref AndroidMemoryWipeInProgressComponent comp)
    {
        comp.Wiper = null;
        comp.Target = null;
        comp.EscapeInProgress = false;

        RemCompDeferred<AndroidMemoryWipeInProgressComponent>(uid);
        _actionBlocker.UpdateCanMove(uid);
    }

    private void OnMemoryWipeAcknowledge(EntityUid uid, AndroidMemoryWipeResultComponent comp, AndroidMemoryWipeAcknowledgeMessage msg)
    {
        if (!msg.Acknowledged)
            return;

        comp.Acknowledged = true;
        _ui.CloseUi(uid, AndroidMemoryWipeUiKey.Key);
        RemCompDeferred<AndroidMemoryWipeResultComponent>(uid);
    }
}

