using Content.Shared.Imperial.WhitelistClothing.Components;
using Content.Shared.Tag;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Content.Server.Imperial.WhitelistClothing.Systems;

public sealed class WhitelistClothingSystems : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DidEquipEvent>(OnEquipAttempt);
    }
    private void OnEquipAttempt(DidEquipEvent args)
    {
        if (TryComp<WhitelistClothingComponent>(args.Equipee, out var component) && component.WhitelistState == "humanoid" && !_tagSystem.HasTag(args.Equipment, component.Whitelist) && args.Slot.Equals("outerclothing", StringComparison.CurrentCultureIgnoreCase))
            _inventorySystem.TryUnequip(args.Equipee, args.Slot);
        else if (TryComp<WhitelistClothingComponent>(args.Equipment, out var componentEquipment) && componentEquipment.WhitelistState == "clothing" && !HasComp<WhitelistClothingComponent>(args.Equipee))
            _inventorySystem.TryUnequip(args.Equipee, args.Slot);
    }
}
