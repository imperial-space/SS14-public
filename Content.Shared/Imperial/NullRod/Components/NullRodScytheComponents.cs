using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Зловещий осколок (cyberimp/arm/toolkit/shard/scythe из SS13): при использовании в руке уходит в руку
/// владельца и позволяет призывать порочную косу.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodScytheShardComponent : Component
{
    [DataField]
    public EntProtoId Scythe = "NullRodVorpalScythe";

    [DataField]
    public SoundSpecifier? ImplantSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");
}

/// <summary>
/// Осколок в руке святого: действие призывает и убирает косу.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodScytheArmComponent : Component
{
    [DataField]
    public EntProtoId Scythe = "NullRodVorpalScythe";

    [DataField]
    public EntProtoId Action = "ActionNullRodToggleScythe";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public EntityUid? ScytheEntity;

    /// <summary>Урон, если убрать косу, не дав ей вкусить крови.</summary>
    [DataField]
    public float RetractDamage = 25f;

    [DataField]
    public SoundSpecifier? ExtendSound = new SoundPathSpecifier("/Audio/Items/unsheath.ogg");

    [DataField]
    public SoundSpecifier? RetractSound = new SoundPathSpecifier("/Audio/Items/sheath.ogg");
}

/// <summary>
/// Порочная коса: насыщается ударами и «похоронным звоном», насыщенную можно убрать без вреда,
/// усиленная бьёт вдвое сильнее.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodVorpalScytheComponent : Component
{
    [ViewVariables]
    public ScytheEmpowerment Empowerment = ScytheEmpowerment.Weak;

    [ViewVariables]
    public TimeSpan EmpowermentEnd;

    /// <summary>4 минуты делятся на уровень насыщения: насыщена 4 минуты, усилена 2.</summary>
    [DataField]
    public TimeSpan EmpowermentDuration = TimeSpan.FromMinutes(4);

    [DataField]
    public float EmpoweredMultiplier = 2f;

    /// <summary>Длительность похоронного звона до модификаторов.</summary>
    [DataField]
    public TimeSpan DeathKnellTime = TimeSpan.FromSeconds(15);
}

public enum ScytheEmpowerment : byte
{
    Weak = 0,
    Sated = 1,
    Empowered = 2,
}

public sealed partial class NullRodToggleScytheActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class NullRodDeathKnellDoAfterEvent : SimpleDoAfterEvent;
