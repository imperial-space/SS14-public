using Content.Server.Imperial.SCP.SCPBlink.Components;
using Content.Server.Popups;
using Content.Shared.Alert;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.SCP.SCPBlink;
using Robust.Shared.Audio.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.SCP.SCPBlink.Systems;

public sealed class SCPBlinkSystem : EntitySystem
{
    private const string BlinkAlert = "SCPBlink";

    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCPBlinkableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCPBlinkableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SCPBlinkableComponent, SCPBlinkAlertEvent>(OnBlinkAlert);
        SubscribeLocalEvent<SCPBlinkBlindnessComponent, CanSeeAttemptEvent>(OnBlinkBlindnessSeeAttempt);
        SubscribeLocalEvent<SCPBlinkManualTriggerComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnBlinkAlert(Entity<SCPBlinkableComponent> ent, ref SCPBlinkAlertEvent args)
    {
        if (args.Handled)
            return;

        TryManualBlink(ent.Owner);
        args.Handled = true;
    }

    private void OnMapInit(Entity<SCPBlinkableComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.IsBlinking = false;
        ent.Comp.NextBlinkTime = _timing.CurTime + ent.Comp.BlinkInterval;
        ent.Comp.BlinkEndTime = TimeSpan.Zero;
        ent.Comp.NextManualBlink = TimeSpan.Zero;
        ent.Comp.NextAlertUpdate = TimeSpan.Zero;
        ent.Comp.VisualBlindEndTime = TimeSpan.Zero;
        ent.Comp.LegacyBlindnessCleanupDone = false;
    }

    private void OnShutdown(Entity<SCPBlinkableComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, BlinkAlert);

        if (HasComp<SCPBlinkBlindnessComponent>(ent.Owner))
        {
            RemComp<SCPBlinkBlindnessComponent>(ent.Owner);
            _blindable.UpdateIsBlind(ent.Owner);
        }

        if (HasComp<TemporaryBlindnessComponent>(ent.Owner))
        {
            RemComp<TemporaryBlindnessComponent>(ent.Owner);
            _blindable.UpdateIsBlind(ent.Owner);
        }
    }

    private void OnBlinkBlindnessSeeAttempt(Entity<SCPBlinkBlindnessComponent> ent, ref CanSeeAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInteractHand(Entity<SCPBlinkManualTriggerComponent> ent, ref InteractHandEvent args)
    {
        if (TryManualBlink(args.User))
            args.Handled = true;
    }

    private bool TryManualBlink(EntityUid uid)
    {
        if (!TryComp<SCPBlinkableComponent>(uid, out var blink))
            return false;

        if (!blink.CanManualBlink)
            return false;

        if (blink.IsBlinking)
            return false;

        var curTime = _timing.CurTime;
        if (curTime < blink.NextManualBlink)
            return false;

        if (!TryComp<MobStateComponent>(uid, out var mobState))
            return false;

        var validState = mobState.CurrentState == MobState.Alive ||
                         (blink.AllowCritical && mobState.CurrentState == MobState.Critical);

        if (!validState)
            return false;

        BeginBlink((uid, blink));
        blink.NextManualBlink = curTime + blink.ManualBlinkCooldown;
        _popup.PopupEntity(blink.ManualBlinkPopup, uid, uid, PopupType.Medium);

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SCPBlinkableComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (!comp.LegacyBlindnessCleanupDone)
            {
                comp.LegacyBlindnessCleanupDone = true;
                if (HasComp<TemporaryBlindnessComponent>(uid))
                {
                    RemComp<TemporaryBlindnessComponent>(uid);
                    _blindable.UpdateIsBlind(uid);
                }
            }

            if (comp.VisualBlindEndTime != TimeSpan.Zero && curTime >= comp.VisualBlindEndTime)
            {
                comp.VisualBlindEndTime = TimeSpan.Zero;
                if (HasComp<SCPBlinkBlindnessComponent>(uid))
                {
                    RemComp<SCPBlinkBlindnessComponent>(uid);
                    _blindable.UpdateIsBlind(uid);
                }
            }

            var validState = mobState.CurrentState == MobState.Alive ||
                             (comp.AllowCritical && mobState.CurrentState == MobState.Critical);

            if (!validState)
                continue;

            if (comp.IsBlinking)
            {
                if (curTime < comp.BlinkEndTime)
                {
                    UpdateBlinkAlert(uid, comp, curTime, comp.BlinkEndTime - curTime);
                    continue;
                }

                comp.IsBlinking = false;
                if (!string.IsNullOrWhiteSpace(comp.BlinkEndPopup))
                    _popup.PopupEntity(comp.BlinkEndPopup, uid, uid, PopupType.SmallCaution);

                if (comp.BlinkEndSound != null)
                    _audio.PlayPvs(comp.BlinkEndSound, uid);

                UpdateBlinkAlert(uid, comp, curTime, comp.NextBlinkTime - curTime);
                continue;
            }

            if (curTime >= comp.NextBlinkTime)
                BeginBlink((uid, comp));

            UpdateBlinkAlert(uid, comp, curTime, comp.NextBlinkTime - curTime);
        }
    }

    private void BeginBlink(Entity<SCPBlinkableComponent> ent)
    {
        var curTime = _timing.CurTime;
        ent.Comp.IsBlinking = true;
        ent.Comp.BlinkEndTime = curTime + ent.Comp.BlinkDuration;
        ent.Comp.NextBlinkTime = curTime + ent.Comp.BlinkInterval;
        if (ent.Comp.VisualBlindDuration > TimeSpan.Zero)
        {
            ent.Comp.VisualBlindEndTime = curTime + ent.Comp.VisualBlindDuration;
            EnsureComp<SCPBlinkBlindnessComponent>(ent.Owner);
            _blindable.UpdateIsBlind(ent.Owner);
        }

        if (!string.IsNullOrWhiteSpace(ent.Comp.BlinkStartPopup))
            _popup.PopupEntity(ent.Comp.BlinkStartPopup, ent, ent, PopupType.SmallCaution);

        if (ent.Comp.BlinkStartSound != null)
            _audio.PlayPvs(ent.Comp.BlinkStartSound, ent);

        UpdateBlinkAlert(ent.Owner, ent.Comp, curTime, ent.Comp.BlinkEndTime - curTime);
    }

    private void UpdateBlinkAlert(EntityUid uid, SCPBlinkableComponent comp, TimeSpan curTime, TimeSpan remaining)
    {
        if (curTime < comp.NextAlertUpdate)
            return;

        comp.NextAlertUpdate = curTime + TimeSpan.FromSeconds(0.2f);
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        _alerts.UpdateAlert(uid, BlinkAlert, cooldown: remaining, showCooldown: true);
    }
}
