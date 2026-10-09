using Content.Client.Overlays;
using Content.Shared.Imperial.TerrorSpider.Components;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.TerrorSpider;

public sealed class TerrorSpiderAuraVisionSystem : EquipmentHudSystem<TerrorSpiderAuraComponent>
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private TerrorSpiderAuraOverlay? _auraOverlay;
    private bool _overlayActive;

    public override void Initialize()
    {
        base.Initialize();
        _auraOverlay = new TerrorSpiderAuraOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<TerrorSpiderAuraComponent> component)
    {
        base.UpdateInternal(component);

        if (_auraOverlay == null)
            return;

        if (!_overlayActive)
        {
            _overlay.AddOverlay(_auraOverlay);
            _overlayActive = true;
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        if (_auraOverlay != null && _overlayActive)
        {
            _overlay.RemoveOverlay(_auraOverlay);
            _overlayActive = false;
        }
    }
}
