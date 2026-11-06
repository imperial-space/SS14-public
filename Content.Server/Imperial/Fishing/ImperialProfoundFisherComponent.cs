using Content.Shared.CombatMode;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Imperial.ModModules;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Перчатки, которые сами служат удочкой (/obj/item/clothing/gloves/fishing + profound_fisher).
/// Атлетические перчатки изматывают во время вываживания и ранят руки при поимке крупной рыбы.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialProfoundFisherComponent : Component
{
    /// <summary>/obj/item/fishing_rod/mob_fisher/athletic.</summary>
    [DataField]
    public bool Athletic;
}
