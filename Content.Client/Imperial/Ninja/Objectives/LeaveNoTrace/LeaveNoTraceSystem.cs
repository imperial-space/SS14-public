
using System.Linq;
using Content.Shared.Imperial.LeaveNoTrace;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.LeaveNoTrace;


public sealed class LeaveNoTraceSystem : SharedLeaveNoTraceSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;


    private RevealOverlay _overlay = default!;


    public override void Initialize()
    {
        base.Initialize();

        _overlay = new RevealOverlay();

        SubscribeLocalEvent<LeaveNoTraceComponent, NinjaHideEvent>(OnHide);
        SubscribeLocalEvent<LeaveNoTraceComponent, NinjaRevealedEvent>(OnRevealed);
        SubscribeLocalEvent<LeaveNoTraceComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<LeaveNoTraceComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LeaveNoTraceComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateRevealAnimation(frameTime);
        UpdateFadeRevealText(frameTime);
    }

    #region Event Handlers

    private void OnRevealed(EntityUid uid, LeaveNoTraceComponent component, NinjaRevealedEvent args)
    {
        if (_playerManager.LocalEntity != uid) return;

        SetupOverlay(component);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnHide(EntityUid uid, LeaveNoTraceComponent component, NinjaHideEvent args)
    {
        if (_playerManager.LocalEntity != uid) return;

        _overlayManager.RemoveOverlay(_overlay);
        ResetOverlay();
    }

    private void OnShutdown(EntityUid uid, LeaveNoTraceComponent component, ComponentShutdown args)
    {
        if (_playerManager.LocalEntity != uid) return;

        _overlay.IsReveal = true;

        var fadeComponent = EnsureComp<RevealOverlayFadeComponent>(uid);
        fadeComponent.RemoveRevealOverlayEndTime = _timing.CurTime + fadeComponent.RemoveRevealOverlayTime;
    }

    private void OnPlayerAttached(EntityUid uid, LeaveNoTraceComponent component, LocalPlayerAttachedEvent args)
    {
        if (!component.IsSeen) return;

        if (_playerManager.LocalEntity != uid) return;
        if (!_overlayManager.AllOverlays.Contains(_overlay)) return;

        SetupOverlay(component);
        _overlayManager.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, LeaveNoTraceComponent component, LocalPlayerDetachedEvent args)
    {
        if (_playerManager.LocalEntity != uid) return;

        _overlayManager.RemoveOverlay(_overlay);
        ResetOverlay();
    }

    #endregion

    #region Helpers

    private void UpdateRevealAnimation(float frameTime)
    {
        if (!TryComp<LeaveNoTraceComponent>(_playerManager.LocalEntity, out var component))
            return;

        if (component.RevealEndTime == null) // Paranoia
            return;

        if (!component.IsSeen)
            return;

        _overlay.RevealProgress = 1.0f - (float)(component.RevealEndTime.Value - _timing.CurTime).TotalSeconds / (float)component.TimeForReveal.TotalSeconds;
    }

    private void UpdateFadeRevealText(float frameTime)
    {
        if (!TryComp<RevealOverlayFadeComponent>(_playerManager.LocalEntity, out var component))
            return;

        _overlay.FadeProgress = 1.0f - (float)(component.RemoveRevealOverlayEndTime - _timing.CurTime).TotalSeconds / (float)component.RemoveRevealOverlayTime.TotalSeconds;

        if (_timing.CurTime <= component.RemoveRevealOverlayEndTime)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        ResetOverlay();
        RemComp<RevealOverlayFadeComponent>(_playerManager.LocalEntity.Value);
    }

    private void ResetOverlay()
    {
        _overlay.IsReveal = false;
        _overlay.RevealProgress = 0.0f;
    }

    private void SetupOverlay(LeaveNoTraceComponent component)
    {
        _overlay.RevealLetter = Loc.GetString(component.RevealText);
        _overlay.TextGlitchEffectParams = component.TextGlitchEffectParams;
        _overlay.TextureParams = component.TextureParams;
    }

    #endregion
}
