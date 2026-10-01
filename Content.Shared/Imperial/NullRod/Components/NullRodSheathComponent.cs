using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.NullRod.Components;

/// <summary>
/// Ножны катаны Ханзо (storage/belt/sheath/hanzo_katana из SS13): на поясе дают действие «Контратака».
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NullRodSheathComponent : Component
{
    /// <summary>Слот ItemSlots с клинком.</summary>
    [DataField]
    public string Slot = "item";

    [DataField]
    public EntProtoId Action = "ActionNullRodCounterattack";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>Сколько стоит на месте, готовясь к контратаке.</summary>
    [DataField]
    public TimeSpan ImmobilizeTime = TimeSpan.FromSeconds(1);

    /// <summary>Сколько длится готовность к контратаке.</summary>
    [DataField]
    public TimeSpan CounterWindow = TimeSpan.FromSeconds(1.5);

    /// <summary>Откат после неудачной попытки.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(45);

    /// <summary>Нельзя контратаковать сразу после того, как клинок убран в ножны.</summary>
    [DataField]
    public TimeSpan ResheathCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public float DamageMultiplier = 3f;

    [DataField]
    public SoundSpecifier? CounterSound = new SoundPathSpecifier("/Audio/Items/unsheath.ogg");

    [ViewVariables]
    public TimeSpan LastResheath;
}

/// <summary>
/// Святой готов контратаковать выбранную цель клинком из ножен.
/// </summary>
[RegisterComponent]
public sealed partial class NullRodCounterStanceComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public EntityUid Sheath;

    [ViewVariables]
    public TimeSpan Expires;

    /// <summary>До какого момента святой стоит на месте (Immobilize в SS13, без оглушения: руки свободны).</summary>
    [ViewVariables]
    public TimeSpan ImmobileUntil;

    [ViewVariables]
    public bool Immobile;

    /// <summary>Удар, который отбит контратакой в этот тик.</summary>
    [ViewVariables]
    public bool Countered;
}

public sealed partial class NullRodCounterattackActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public enum NullRodSheathVisuals : byte
{
    Full,
}
