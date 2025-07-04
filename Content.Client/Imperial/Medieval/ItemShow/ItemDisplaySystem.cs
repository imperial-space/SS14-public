using Content.Shared.Imperial.Medieval.ItemShow;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Medieval.ItemShow;

public sealed class ItemDisplaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
    }

    public void RequestItemPreview(EntityUid itemUid)
    {
        var netEntity = GetNetEntity(itemUid);

        var ev = new ItemDisplayRequest(netEntity);

        RaiseNetworkEvent(ev);
    }

    private void OnAttached(LocalPlayerAttachedEvent ev)
    {
        var overlay = new ItemDisplayOverlay();

        _overlayManager.AddOverlay(overlay);
    }

    private void OnDetached(LocalPlayerDetachedEvent ev)
    {
        _overlayManager.RemoveOverlay<ItemDisplayOverlay>();
    }
}
