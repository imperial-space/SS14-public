using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaChaosGod : byte
{
    None,
    Khorne,
    Nurgle,
    Tzeentch,
    Slaanesh,
}

/// <summary>
/// Демон: пока пилоны стоят, варп слаб — он бьёт слабее и получает больше урона. После смерти возвращается
/// в варп (тело исчезает), розовые ужасы при этом делятся на синих, а гибель принца демонов решает войну.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaDaemonComponent : Component
{
    [DataField]
    public CadiaChaosGod God;

    /// <summary>Во что распадается при смерти.</summary>
    [DataField]
    public EntProtoId? SplitInto;

    [DataField]
    public int SplitCount;

    /// <summary>Босс: его изгнание — победа Империума.</summary>
    [DataField]
    public bool Boss;

    /// <summary>Летает между этажами.</summary>
    [DataField]
    public bool Flyer;
}

/// <summary>Варп-разлом Хаоса: здесь колдуны и апостолы тратят силу варпа на призыв.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaWarpRiftComponent : Component
{
    [DataField]
    public List<CadiaSummonEntry> Summons = new();

    /// <summary>Малый разлом закрывается сам, большой стоит до конца.</summary>
    [DataField]
    public TimeSpan? Lifetime;

    [ViewVariables]
    public TimeSpan? ClosesAt;
}

[DataDefinition]
public sealed partial class CadiaSummonEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public int Cost = 10;

    /// <summary>Открывается только после падения половины пилонов или в развязке.</summary>
    [DataField]
    public bool RequiresFallenPylons;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);
}

/// <summary>Может проводить призывы у разлома.</summary>
[RegisterComponent]
public sealed partial class CadiaSummonerComponent : Component;

[Serializable, NetSerializable]
public sealed partial class CadiaSummonDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Index;

    public CadiaSummonDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>Действия «подняться/опуститься» летающего демона и прыжкового ранца.</summary>
public sealed partial class CadiaFlyUpActionEvent : InstantActionEvent;

public sealed partial class CadiaFlyDownActionEvent : InstantActionEvent;

/// <summary>Метка бога Хаоса на десантнике.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaChaosMarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaChaosGod God;

    /// <summary>Щит варпа Тзинча: следующее попадание поглощается.</summary>
    [ViewVariables]
    public TimeSpan NextWard;

    [ViewVariables]
    public List<EntityUid> ChoiceActions = new();
}

public sealed partial class CadiaChooseMarkActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public CadiaChaosGod God;
}

/// <summary>Может принять метку: выдаёт четыре действия выбора, одно на бога.</summary>
[RegisterComponent]
public sealed partial class CadiaMarkCandidateComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Actions = new();
}
