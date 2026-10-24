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

public enum ImperialModularShieldModuleKind : byte
{
    Node,
    Cable,
    Charger,
    Relay,
    Well,
}

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

/// <summary>Клетка модульного щита (/obj/structure/emergency_shield/modular).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModularShieldComponent : Component
{
    [ViewVariables]
    public EntityUid? Generator;
}

/// <summary>Консоль модульных щитов (/obj/machinery/computer/modular_shield).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModularShieldConsoleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Generators = new();
}

/// <summary>Мультитул запомнил генератор щита (buffer).</summary>
[RegisterComponent]
public sealed partial class ImperialModularShieldBufferComponent : Component
{
    [ViewVariables]
    public EntityUid Generator;
}

[Serializable, NetSerializable]
public enum ImperialModularShieldVisuals : byte
{
    /// <summary>Готовое состояние спрайта (gen_ready_closed, charger_on_open, cable_node_closed_r_nb_l и т. п.).</summary>
    State,

    /// <summary>Прозрачность клетки щита (0..1).</summary>
    Alpha,
}

[Serializable, NetSerializable]
public enum ImperialModularShieldUiKey : byte
{
    Generator,
    Console,
}

[Serializable, NetSerializable]
public sealed class ImperialModularShieldGeneratorState : BoundUserInterfaceState
{
    public float MaxRadius;
    public float CurrentRadius;
    public float MaxStrength;
    public float CurrentStrength;
    public float MaxRegeneration;
    public float CurrentRegeneration;
    public bool Active;
    public bool Recovering;
    public bool ExteriorOnly;
    public bool Initiating;
}

[Serializable, NetSerializable]
public sealed class ImperialModularShieldConsoleEntry
{
    public int Id;
    public string Name = string.Empty;
    public float MaxStrength;
    public float CurrentStrength;
    public float CurrentRegeneration;
    public bool Active;
    public bool Recovering;
    public bool Inactive;
}

[Serializable, NetSerializable]
public sealed class ImperialModularShieldConsoleState(List<ImperialModularShieldConsoleEntry> generators) : BoundUserInterfaceState
{
    public readonly List<ImperialModularShieldConsoleEntry> Generators = generators;
}

[Serializable, NetSerializable]
public sealed class ImperialModularShieldSetRadiusMessage(float radius) : BoundUserInterfaceMessage
{
    public readonly float Radius = radius;
}

[Serializable, NetSerializable]
public sealed class ImperialModularShieldToggleMessage(int id = -1) : BoundUserInterfaceMessage
{
    /// <summary>Номер генератора в консоли; −1 — сам генератор.</summary>
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class ImperialModularShieldToggleExteriorMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ImperialModularShieldRenameMessage(int id, string name) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public enum ImperialModularShieldWireStatus : byte
{
    Key,
}
