using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Пицца-бомба (pizzabox/bomb): при первом открытии ставится таймер, при следующем — обратный отсчёт.</summary>
[RegisterComponent]
public sealed partial class UplinkPizzaBombComponent : Component
{
    [DataField]
    public int Timer = 1;

    [ViewVariables]
    public bool Armed;

    [ViewVariables]
    public bool Open;

    [ViewVariables]
    public TimeSpan? DetonateAt;
}

[Serializable, NetSerializable]
public enum UplinkPizzaBombVisuals : byte
{
    State,
}

/// <summary>Детонатор Синдиката: все взведённые бомбы Синдиката взрываются через 5 секунд.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicateDetonatorComponent : Component
{
    [ViewVariables]
    public TimeSpan ReadyAt;
}

/// <summary>Землеразлом E-2.</summary>
[RegisterComponent]
public sealed partial class UplinkEarthcrackerComponent : Component
{
    [ViewVariables]
    public UplinkEarthcrackerStatus Status = UplinkEarthcrackerStatus.Ready;

    [ViewVariables]
    public TimeSpan? ActivateAt;
}

[Serializable, NetSerializable]
public enum UplinkEarthcrackerStatus : byte
{
    Ready,
    Active,
    Spent,
}

/// <summary>Слабое место в полу (effect/weakpoint): взрыв рядом раскалывает пол цепочкой трещин.</summary>
[RegisterComponent]
public sealed partial class UplinkWeakpointComponent : Component
{
    [DataField]
    public int CrackLength = 8;

    [DataField]
    public int NewWeakpoints = 2;

    [DataField]
    public bool SpawnsChildren = true;
}

/// <summary>Граната грибкового туберкулёза: заражает всех в облаке.</summary>
[RegisterComponent]
public sealed partial class UplinkDiseaseGrenadeComponent : Component
{
    [DataField(required: true)]
    public string Disease = string.Empty;

    [DataField]
    public float Range = 5f;
}

/// <summary>Имплант S3: картонная коробка-хамелеон по нажатию.</summary>
[RegisterComponent]
public sealed partial class UplinkStealthImplantComponent : Component
{
    [DataField]
    public EntProtoId Box = "StealthBox";
}

public sealed partial class UplinkDeployBoxActionEvent : InstantActionEvent;

/// <summary>Имплант-ревивер: лечит и реанимирует, пока носитель без сознания.</summary>
[RegisterComponent]
public sealed partial class UplinkReviverImplantComponent : Component
{
    [ViewVariables]
    public bool Reviving;

    [ViewVariables]
    public float ReviveCost;

    [ViewVariables]
    public TimeSpan ReadyAt;

    [ViewVariables]
    public TimeSpan NextHeal;

    [ViewVariables]
    public TimeSpan DefibAt;
}

/// <summary>Имплант-перезагрузчик ЦНС: через секунду после оглушения снимает его и даёт 6 секунд иммунитета.</summary>
[RegisterComponent]
public sealed partial class UplinkCnsRebooterComponent : Component
{
    [ViewVariables]
    public TimeSpan ReadyAt;

    [ViewVariables]
    public TimeSpan? ClearAt;

    [ViewVariables]
    public TimeSpan ImmuneUntil;
}

/// <summary>Имплант тактического отрицания: без крита, но через 10 секунд после «крита» — взрыв.</summary>
[RegisterComponent]
public sealed partial class UplinkDeniabilityImplantComponent : Component
{
    [ViewVariables]
    public FixedPoint2? OriginalCrit;

    [ViewVariables]
    public TimeSpan? DetonateAt;
}

/// <summary>Рентгеновские глаза: видят сквозь стены.</summary>
[RegisterComponent]
public sealed partial class UplinkXrayEyesComponent : Component;

/// <summary>Диск Детоматикс: подрывает КПК членов экипажа.</summary>
[RegisterComponent]
public sealed partial class UplinkDetomatixComponent : Component
{
    [DataField]
    public int Charges = 4;
}
