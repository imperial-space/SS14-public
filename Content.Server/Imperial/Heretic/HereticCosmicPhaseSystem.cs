using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Mind;
using Content.Shared.Atmos;
using Content.Shared.Eye;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmicPhaseSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem    _atmosphere  = default!;
    [Dependency] private readonly MindSystem          _mind        = default!;
    [Dependency] private readonly SharedPopupSystem   _popup       = default!;
    [Dependency] private readonly SharedAudioSystem   _audio       = default!;
    [Dependency] private readonly SharedTransformSystem _xform     = default!;
    [Dependency] private readonly VisibilitySystem    _visibility  = default!;
    [Dependency] private readonly SharedEyeSystem     _eye         = default!;
    [Dependency] private readonly RespiratorSystem    _respirator  = default!;

    private static readonly SoundPathSpecifier PhaseSound =
        new("/Audio/Imperial/heretic/sound_magic_cosmic_energy.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticCosmicPhaseActionEvent>(OnEnterPhase);
        SubscribeLocalEvent<HereticCosmicGhostComponent, HereticCosmicPhaseExitActionEvent>(OnExitPhase);
        SubscribeLocalEvent<HereticCosmicGhostComponent, EntityTerminatingEvent>(OnGhostTerminating);
        SubscribeLocalEvent<HereticCosmicGhostComponent, GetVisMaskEvent>(OnGhostGetVisMask);
        SubscribeLocalEvent<HereticCosmicPhaseComponent, ComponentShutdown>(OnPhaseShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<HereticCosmicPhaseComponent>();
        while (query.MoveNext(out var uid, out _))
            _respirator.UpdateSaturation(uid, 10f);
    }

    private bool IsInLowPressure(EntityUid uid)
    {
        var mix = _atmosphere.GetTileMixture(uid);
        return mix == null || mix.Pressure <= Atmospherics.WarningLowPressure;
    }

    private void OnEnterPhase(EntityUid uid, HereticComponent comp, HereticCosmicPhaseActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        if (HasComp<HereticCosmicPhaseComponent>(uid))
            return;

        if (!IsInLowPressure(uid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-cosmic-phase-invalid"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return;

        var ghostUid = Spawn("MobHereticCosmicGhost", Transform(uid).Coordinates);

        if (!TryComp<HereticCosmicGhostComponent>(ghostUid, out var ghostComp))
        {
            QueueDel(ghostUid);
            return;
        }

        ghostComp.HereticUid = uid;

        var vis = EnsureComp<VisibilityComponent>(ghostUid);
        _visibility.AddLayer((ghostUid, vis), (int) VisibilityFlags.Ghost, false);
        _visibility.RemoveLayer((ghostUid, vis), (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(ghostUid, visibilityComponent: vis);

        EnsureComp<PressureImmunityComponent>(uid);
        _xform.DetachParentToNull(uid, Transform(uid));

        _mind.TransferTo(mindId, ghostUid, ghostCheckOverride: true);
        _eye.RefreshVisibilityMask(ghostUid);

        var phaseComp = EnsureComp<HereticCosmicPhaseComponent>(uid);
        phaseComp.CosmicGhostUid = ghostUid;

        _audio.PlayPvs(PhaseSound, ghostUid);
        _popup.PopupEntity(Loc.GetString("heretic-cosmic-phase"), ghostUid, ghostUid, PopupType.Medium);
    }

    private void OnExitPhase(EntityUid ghostUid, HereticCosmicGhostComponent comp, HereticCosmicPhaseExitActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        if (!IsInLowPressure(ghostUid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-cosmic-phase-exit-invalid"), ghostUid, ghostUid, PopupType.SmallCaution);
            return;
        }

        ExitCosmicPhase(ghostUid, comp);
    }

    private void OnGhostTerminating(EntityUid ghostUid, HereticCosmicGhostComponent comp, ref EntityTerminatingEvent args)
    {
        ExitCosmicPhase(ghostUid, comp, silent: true);
    }

    private void OnGhostGetVisMask(EntityUid uid, HereticCosmicGhostComponent comp, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Ghost;
    }

    private void OnPhaseShutdown(EntityUid uid, HereticCosmicPhaseComponent comp, ComponentShutdown args)
    {
        if (comp.CosmicGhostUid.HasValue && Exists(comp.CosmicGhostUid.Value))
            QueueDel(comp.CosmicGhostUid.Value);
        RemComp<PressureImmunityComponent>(uid);
    }

    private void ExitCosmicPhase(EntityUid ghostUid, HereticCosmicGhostComponent comp, bool silent = false)
    {
        var hereticUid = comp.HereticUid;

        if (!_mind.TryGetMind(ghostUid, out var mindId, out _))
        {
            QueueDel(ghostUid);
            return;
        }

        _xform.SetCoordinates(hereticUid, Transform(ghostUid).Coordinates);
        _mind.TransferTo(mindId, hereticUid, ghostCheckOverride: true);

        if (TryComp<HereticCosmicPhaseComponent>(hereticUid, out var phaseComp))
            phaseComp.CosmicGhostUid = null;
        RemCompDeferred<HereticCosmicPhaseComponent>(hereticUid);

        QueueDel(ghostUid);

        if (!silent)
        {
            _audio.PlayPvs(PhaseSound, hereticUid);
            _popup.PopupEntity(Loc.GetString("heretic-cosmic-phase-exit"), hereticUid, hereticUid, PopupType.Medium);
        }
    }
}
