using Content.Client.Overlays;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticBreachGrayScaleSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager  _player     = default!;

    private BlackAndWhiteOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new BlackAndWhiteOverlay();
        SubscribeLocalEvent<BreachGrayScaleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BreachGrayScaleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, BreachGrayScaleComponent _, ComponentStartup args)
    {
        if (_player.LocalEntity != uid)
            return;
        _overlayMan.AddOverlay(_overlay!);
    }

    private void OnShutdown(EntityUid uid, BreachGrayScaleComponent _, ComponentShutdown args)
    {
        if (_player.LocalEntity != uid)
            return;
        _overlayMan.RemoveOverlay(_overlay!);
    }
}
