using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

/// <summary>Модуль сети щита (/obj/machinery/modular_shield/module/*).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModularShieldModuleComponent : Component
{
    [DataField(required: true)]
    public ImperialModularShieldModuleKind Kind;

    /// <summary>allow_boosters: к узлу можно подключать усилители (у кабеля — нельзя).</summary>
    [DataField]
    public bool AllowBoosters = true;

    /// <summary>is_booster.</summary>
    public bool IsBooster => Kind is ImperialModularShieldModuleKind.Charger or ImperialModularShieldModuleKind.Relay or ImperialModularShieldModuleKind.Well;

    [ViewVariables] public EntityUid? ShieldGenerator;
    [ViewVariables] public EntityUid? ConnectedNode;
    [ViewVariables] public HashSet<EntityUid> ConnectedThroughUs = new();

    /// <summary>charge_boost / range_boost / strength_boost.</summary>
    [ViewVariables] public float Boost;

    [ViewVariables] public Angle LastRotation;
}
