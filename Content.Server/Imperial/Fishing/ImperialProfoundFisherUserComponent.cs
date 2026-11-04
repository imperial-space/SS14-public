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

/// <summary>Владелец может рыбачить пустой рукой этой удочкой (TRAIT_PROFOUND_FISHER).</summary>
[RegisterComponent]
public sealed partial class ImperialProfoundFisherUserComponent : Component
{
    [ViewVariables]
    public EntityUid Rod;

    /// <summary>Перчатки или модуль, давшие способность.</summary>
    [ViewVariables]
    public EntityUid Source;

    [ViewVariables]
    public float DifficultyModifier;

    [ViewVariables]
    public bool Athletic;
}
