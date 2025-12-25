using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.SmithingSystem;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.SmithingSystem;

public sealed partial class SmithingSystem
{
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly DamageOnInteractSystem _damageOnInteract = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;


    protected override void InitializeFurnaceSystem()
    {
        base.InitializeFurnaceSystem();

        SubscribeLocalEvent<SmithingFurnaceComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<SmithingFurnaceComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    private void FurnanceUpdate()
    {
        var query = EntityQueryEnumerator<SmithingFurnaceComponent>();

        while (query.MoveNext(out var uid, out var furnaceComponent))
        {
            if (!furnaceComponent.UnlockTime.HasValue ||
                furnaceComponent.UnlockTime > _gameTiming.CurTime)
                continue;

            _itemSlots.SetLock(uid, furnaceComponent.MeltingSlot, false);
            _itemSlots.TryEject(uid, furnaceComponent.MeltingSlot, null, out _);

            furnaceComponent.UnlockTime = null;
        }
    }

    private void OnEntInserted(Entity<SmithingFurnaceComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        _itemSlots.SetLock(ent, ent.Comp.MeltingSlot, true);
        ent.Comp.UnlockTime = _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp.MeltingTime);
    }

    private void OnEntRemoved(Entity<SmithingFurnaceComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!TryComp<SmithingWorkpieceComponent>(args.Entity, out var workpieceComponent))
            return;

        workpieceComponent.ReadyToForge = true;
        Dirty(args.Entity, workpieceComponent);

        _appearanceSystem.SetData(args.Entity, WorkpieceVisuals.Melted, true);

        if (TryComp<DamageOnInteractComponent>(args.Entity, out var damageOnInteractComp))
            _damageOnInteract.SetIsDamageActiveTo((args.Entity, damageOnInteractComp), true);
    }
}
