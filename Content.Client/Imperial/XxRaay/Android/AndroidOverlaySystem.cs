using Content.Shared.Imperial.XxRaay.Android;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Imperial.XxRaay.Android;

/// <summary>
/// Включает клиентский оверлей
/// </summary>
public sealed partial class AndroidOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMgr = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private AndroidOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidOverlayComponent, ComponentInit>(OnOverlayInit);
        SubscribeLocalEvent<AndroidOverlayComponent, ComponentRemove>(OnOverlayRemove);
        SubscribeLocalEvent<AndroidOverlayComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AndroidOverlayComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        RemoveOverlay();
    }

    private void OnOverlayInit(Entity<AndroidOverlayComponent> ent, ref ComponentInit args)
    {
        var local = _player.LocalEntity;
        if (local != ent)
            return;

        AddOverlay(ent);
    }

    private void OnOverlayRemove(Entity<AndroidOverlayComponent> ent, ref ComponentRemove args)
    {
        var local = _player.LocalEntity;
        if (local != ent)
            return;

        RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<AndroidOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        AddOverlay(ent);
    }

    private void OnPlayerDetached(Entity<AndroidOverlayComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void AddOverlay(Entity<AndroidOverlayComponent> ent)
    {
        if (_overlay != null)
            return;

        _overlay = new AndroidOverlay(ent.Comp.IconSprite, ent.Comp.IconScale);
        _overlayMgr.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayMgr.RemoveOverlay(_overlay);
        _overlay = null;
    }
}

