using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Защита, которую модуль добавляет части костюма (rad_protection и т. п.).
/// Не конфликтует с собственной Armor части.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModArmorComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();
}
