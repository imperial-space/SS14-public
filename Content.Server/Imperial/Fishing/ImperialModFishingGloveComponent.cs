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

/// <summary>Модуль МОДа «рыболовная перчатка» (/obj/item/mod/module/fishing_glove): удочка вставляется в модуль.</summary>
[RegisterComponent]
public sealed partial class ImperialModFishingGloveComponent : Component
{
    public const string SlotId = "mod_fishing_rod";

    [DataField]
    public float DifficultyModifier = -5;

    [ViewVariables]
    public EntityUid? Wearer;
}
