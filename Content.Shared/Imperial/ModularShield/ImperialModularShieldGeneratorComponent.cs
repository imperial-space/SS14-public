using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModularShield;

/// <summary>
/// Генератор модульного щита (/obj/machinery/modular_shield_generator) или щитовой шлюз (gate).
/// Логика — на сервере, здесь только данные.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModularShieldGeneratorComponent : Component
{
    /// <summary>Шлюз: плоская стена вперёд вместо купола, без штрафа за внутренние клетки.</summary>
    [DataField]
    public bool Gate;

    /// <summary>internal_penalty.</summary>
    [DataField]
    public bool InternalPenalty = true;

    /// <summary>icon_type: gen или gate.</summary>
    [DataField]
    public string IconType = "gen";

    [DataField]
    public string DisplayName = "Генератор щита";

    [ViewVariables] public bool Active;
    [ViewVariables] public bool Initiating;
    [ViewVariables] public bool Recovering = true;
    [ViewVariables] public float MaxStrength = 40;
    [ViewVariables] public float StoredStrength;
    [ViewVariables] public float MaxRegeneration = 3;
    [ViewVariables] public float CurrentRegeneration;
    [ViewVariables] public float MaxRadius = 3;
    [ViewVariables] public float Radius = 3;
    [ViewVariables] public bool ExteriorOnly;

    [ViewVariables] public float RegenBoost;
    [ViewVariables] public float RadiusBoost;
    [ViewVariables] public float MaxStrengthBoost;

    [ViewVariables] public float InnateRegen = 3;
    [ViewVariables] public float InnateRadius = 3;
    [ViewVariables] public float InnateStrength = 40;

    [ViewVariables] public List<EntityUid> DeployedShields = new();
    [ViewVariables] public HashSet<EntityUid> ConnectedModules = new();
    [ViewVariables] public TimeSpan? FinishFieldAt;
}
