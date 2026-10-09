using Content.Client.Overlays;
using Content.Shared.Imperial.Lavaland.MesonVision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Lavaland.MesonVision;

public sealed class MesonVisionSystem : EquipmentHudSystem<MesonVisionComponent>
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    private MesonVisionOverlay? _mesonOverlay;
    private bool _overlayActive;

    public override void Initialize()
    {
        base.Initialize();
        _mesonOverlay = new MesonVisionOverlay();
        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<MesonVisionComponent>>(OnRelayInventory);
    }

    private void OnRelayInventory(EntityUid uid, InventoryComponent comp, ref RefreshEquipmentHudEvent<MesonVisionComponent> args)
    {
        _inventorySystem.RelayEvent((uid, comp), ref args);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<MesonVisionComponent> component)
    {
        base.UpdateInternal(component);

        if (_mesonOverlay == null)
            return;

        if (!_overlayActive)
        {
            _overlay.AddOverlay(_mesonOverlay);
            _overlayActive = true;
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        if (_mesonOverlay != null && _overlayActive)
        {
            _overlay.RemoveOverlay(_mesonOverlay);
            _overlayActive = false;
        }
    }
}
