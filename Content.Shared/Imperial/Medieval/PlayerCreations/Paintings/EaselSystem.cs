using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;

public sealed class EaselSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EaselComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EaselComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<EaselComponent, EntRemovedFromContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<EaselComponent, InteractUsingEvent>(OnInteract);

        Subs.BuiEvents<EaselComponent>(EaselUiKey.Key, subs =>
        {
            subs.Event<EaselSaveMessage>(OnSave);
            subs.Event<EaselSendPaintingMessage>(OnSend);
        });
    }

    private void OnSend(EntityUid uid, EaselComponent comp, EaselSendPaintingMessage args)
    {
        if (!TryGetCanvas((uid, comp), out var canvas))
            return;

        RaiseLocalEvent(new SendCreationPaintingEvent(canvas.Texture, args.Name, args.Description, args.Author, args.SenderPlayer));
    }

    private void OnSave(EntityUid uid, EaselComponent comp, EaselSaveMessage args)
    {
        if (!TryGetCanvas((uid, comp), out var canvas))
            return;

        canvas.Texture = args.Texture;
        canvas.Dirty();
        RaiseNetworkEvent(new CanvasTextureChangedEvent(GetNetEntity(canvas.Owner), canvas.Texture));
    }

    private void OnStartup(Entity<EaselComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<EaselComponent> ent)
    {
        _appearance.SetData(ent, EaselVisuals.ContainsItem, HasItem(ent));

        if(!HasItem(ent))
            _ui.CloseUi(ent.Owner, EaselUiKey.Key);
    }

    private void OnInteract(Entity<EaselComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_ui.IsUiOpen(ent.Owner, EaselUiKey.Key))
            return;

        if (!TryGetCanvas(ent, out var canvas))
            return;

        args.Handled = true;

        if (!HasComp<PaintKitComponent>(args.Used))
            return;

        _ui.OpenUi(ent.Owner, EaselUiKey.Key, args.User);

        UpdateUiState(ent.Owner, canvas.Texture);
    }

    private void UpdateUiState(EntityUid uid, Color[] texture)
    {
        _ui.SetUiState(uid, EaselUiKey.Key, new EaselBoundUserInterfaceState(texture));
    }

    private void OnContainerModified(EntityUid uid, EaselComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID == component.Slot)
            UpdateAppearance((uid, component));
    }

    public bool TryGetSlot(Entity<EaselComponent> ent, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;
        if (!TryComp<ItemSlotsComponent>(ent, out var slots))
            return false;

        return _slots.TryGetSlot(ent, ent.Comp.Slot, out slot, slots);
    }

    public bool HasItem(Entity<EaselComponent> ent)
    {
        return TryGetSlot(ent, out var slot) && slot.HasItem;
    }

    public bool TryGetCanvas(Entity<EaselComponent> ent, [NotNullWhen(true)] out CanvasComponent? canvasComponent)
    {
        canvasComponent = null;

        if (!TryGetSlot(ent, out var slot))
            return false;

        if (!slot.HasItem)
            return false;

        var item = slot.Item;

        return TryComp(item, out canvasComponent);
    }
}
