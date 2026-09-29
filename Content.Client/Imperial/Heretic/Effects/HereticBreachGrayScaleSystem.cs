using Content.Client.Overlays;
using Content.Shared.Imperial.Heretic.Reality;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.Imperial.Heretic.Effects;

public sealed class HereticBreachGrayScaleSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private BlackAndWhiteOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new BlackAndWhiteOverlay();
        SubscribeLocalEvent<BreachGrayScaleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BreachGrayScaleComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnStartup(Entity<BreachGrayScaleComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _overlayMan.AddOverlay(_overlay);
    }

    private void OnShutdown(Entity<BreachGrayScaleComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity != ent.Owner)
            return;

        _overlayMan.RemoveOverlay(_overlay);
    }
}
