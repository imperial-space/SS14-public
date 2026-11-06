using Content.Client.Overlays;
using Content.Shared.Imperial.Vision;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Vision;

/// <summary>
/// Ночное зрение: пока оно активно, в карту освещения добавляется ровный свет.
/// Лампы и тени работают как обычно, но тени больше не чёрные.
/// </summary>
public sealed class ImperialNightVisionSystem : EquipmentHudSystem<ImperialNightVisionComponent>
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private ImperialNightVisionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new ImperialNightVisionOverlay();
        SubscribeLocalEvent<InventoryComponent, RefreshEquipmentHudEvent<ImperialNightVisionComponent>>(OnRelayInventory);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnRelayInventory(EntityUid uid, InventoryComponent comp, ref RefreshEquipmentHudEvent<ImperialNightVisionComponent> args)
    {
        _inventory.RelayEvent((uid, comp), ref args);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ImperialNightVisionComponent> args)
    {
        base.UpdateInternal(args);

        var light = Color.Black;
        foreach (var comp in args.Components)
        {
            var c = Color.FromSrgb(comp.MinimumLight);
            light = new Color(MathF.Max(light.R, c.R), MathF.Max(light.G, c.G), MathF.Max(light.B, c.B), 1f);
        }

        _overlay.Light = light;
        if (!_overlays.HasOverlay<ImperialNightVisionOverlay>())
            _overlays.AddOverlay(_overlay);
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _overlays.RemoveOverlay(_overlay);
    }
}
