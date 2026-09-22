using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Components;

/// <summary>
///     Applies the specified DamageModifierSets when the entity takes damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DamageProtectionBuffComponent : Component
{
    /// <summary>
    ///     The damage modifiers for entities with this component.
    ///     Keys are arbitrary names; values are IDs of damageModifierSet prototypes.
    /// </summary>
    [DataField]
    public Dictionary<string, ProtoId<DamageModifierSetPrototype>> Modifiers = new();
}
