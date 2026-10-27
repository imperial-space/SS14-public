using Content.Client.Eye;
using Content.Shared.Imperial.ZLevels;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Imperial.ZLevels;

/// <summary>Подключает отрисовку этажа ниже сквозь прозрачные клетки и взгляд на соседний этаж.</summary>
public sealed class ZLevelSystem : SharedZLevelSystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    private ZLevelOverlay? _overlay;
    private ZLevelLookOverlay? _lookOverlay;
    private ZLevelLightOverlay? _lightOverlay;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(EyeLerpingSystem));
        UpdatesAfter.Add(typeof(SharedEyeSystem));
        _overlay = new ZLevelOverlay();
        _lookOverlay = new ZLevelLookOverlay();
        _overlays.AddOverlay(_overlay);
        _overlays.AddOverlay(_lookOverlay);
        _lightOverlay = new ZLevelLightOverlay();
        _overlays.AddOverlay(_lightOverlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
        if (_lookOverlay != null)
            _overlays.RemoveOverlay(_lookOverlay);
        if (_lightOverlay != null)
            _overlays.RemoveOverlay(_lightOverlay);
        _overlay?.Dispose();
        _lightOverlay?.Dispose();
        _lookOverlay?.Dispose();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (_overlay == null || _lookOverlay == null)
            return;
        // Камера кадра уже сдвинута — подтягиваем к ней глаза вьюпортов этажей до рендера.
        var eye = _eyeManager.CurrentEye;
        var source = eye;
        if (TryGetLookedMap(out var looked, out var up) && _lookOverlay.PrepareEye(eye, looked) is { } lookEye && !up)
            source = lookEye;
        _overlay.PrepareEyes(source);
    }

    /// <summary>Этаж, на который сейчас смотрит локальный игрок, если смотрит.</summary>
    public bool TryGetLookedMap(out MapId map) => TryGetLookedMap(out map, out _);

    public bool TryGetLookedMap(out MapId map, out bool up)
    {
        map = MapId.Nullspace;
        up = false;
        if (_player.LocalEntity is not { } player
            || !TryComp<ZLookingComponent>(player, out var looking)
            || looking.Map is not { } mapUid
            || !TryComp<MapComponent>(mapUid, out var mapComp))
            return false;
        map = mapComp.MapId;
        up = looking.Up;
        return map != MapId.Nullspace;
    }
}
