using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Дисциплины псайкеров Империума и Хаоса.</summary>
[Serializable, NetSerializable]
public enum CadiaPsyPower : byte
{
    Smite,
    Dome,
    Scream,
    Foresight,
    Suppression,
    Fury,
    Warpfire,
    Veil,
    Plague,
    Bloodboil,
    Charm,
    Portal,
}

/// <summary>
/// Псайкер: шкала варп-заряда восстанавливается сама, а напряжение растёт с каждым касто и спадает медленно.
/// Чем выше напряжение, тем вероятнее Опасности варпа. Капюшон и молитва священника снижают риск.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaPsykerComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Charge = 100f;

    [DataField, AutoNetworkedField]
    public float MaxCharge = 100f;

    /// <summary>Восстановление заряда в секунду.</summary>
    [DataField]
    public float Regen = 2.5f;

    /// <summary>Напряжение 0..100: чем выше, тем опаснее следующий каст.</summary>
    [DataField, AutoNetworkedField]
    public float Strain;

    [DataField]
    public float StrainDecay = 1f;

    /// <summary>Подавлен другим псайкером до этого момента.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SilencedUntil;

    /// <summary>Какие способности выдать при появлении.</summary>
    [DataField]
    public List<EntProtoId> Powers = new();

    [ViewVariables]
    public List<EntityUid> Actions = new();
}

/// <summary>Сила на сущности-действии: что делает, сколько заряда стоит и сколько напряжения даёт.</summary>
[RegisterComponent]
public sealed partial class CadiaPsyPowerComponent : Component
{
    [DataField(required: true)]
    public CadiaPsyPower Power;

    [DataField]
    public float Cost = 20f;

    [DataField]
    public float Strain = 15f;
}

/// <summary>Псайкический капюшон: вдвое снижает риск Опасностей варпа.</summary>
[RegisterComponent]
public sealed partial class CadiaPsychicHoodComponent : Component;

/// <summary>
/// Гасит варп рядом: пилон делает силы Хаоса дороже и опаснее, а неприкасаемый полностью глушит чужие силы.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaNullZoneComponent : Component
{
    [DataField]
    public float Radius = 8f;

    /// <summary>Полное подавление (неприкасаемый); иначе только штраф псайкерам Хаоса (пилон).</summary>
    [DataField]
    public bool Total;
}

/// <summary>Телекинетический купол и завеса: снимается по таймеру.</summary>
[RegisterComponent]
public sealed partial class CadiaVeilComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

public sealed partial class CadiaPsyInstantEvent : InstantActionEvent;

public sealed partial class CadiaPsyEntityEvent : EntityTargetActionEvent;

public sealed partial class CadiaPsyWorldEvent : WorldTargetActionEvent;
