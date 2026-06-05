using Content.Client.Overlays;
using Content.Shared.Imperial.Lavaland.ColossusLoot;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Lavaland.Colossus;

public sealed class ThermalEntityVisionSystem : EquipmentHudSystem<ThermalEntityVisionComponent>
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    private ThermalEntityVisionOverlay? _thermalOverlay;
    private bool _overlayActive;

    public override void Initialize()
    {
        base.Initialize();
        _thermalOverlay = new ThermalEntityVisionOverlay();
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ThermalEntityVisionComponent> component)
    {
        base.UpdateInternal(component);

        if (_thermalOverlay == null)
            return;

        if (!_overlayActive)
        {
            _overlay.AddOverlay(_thermalOverlay);
            _overlayActive = true;
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        if (_thermalOverlay != null && _overlayActive)
        {
            _overlay.RemoveOverlay(_thermalOverlay);
            _overlayActive = false;
        }
    }
}
