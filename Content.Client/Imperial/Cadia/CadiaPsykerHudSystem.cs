using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Cadia;

/// <summary>Шкалы псайкера над панелью действий: варп-заряд и напряжение, чем оно выше — тем опаснее каст.</summary>
public sealed class CadiaPsykerHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    private CadiaPsykerHudOverlay? _hud;

    public override void Initialize()
    {
        base.Initialize();
        _hud = new CadiaPsykerHudOverlay();
        _overlays.AddOverlay(_hud);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_hud != null)
            _overlays.RemoveOverlay(_hud);
    }
}
