using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Когтистая капсула (Dreadclaw) над полем: падает с неба, высаживает отделение Хаоса и уходит обратно на флагман.
/// Самого корабля нет — клиент поднимает и опускает спрайт капсулы над её тенью по времени начала манёвра.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaDropPodComponent : Component
{
    /// <summary>Начало спуска или подъёма.</summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan Start;

    /// <summary>Капсула уходит вверх (после высадки), иначе падает.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Leaving;

    /// <summary>Сколько секунд длится спуск или подъём.</summary>
    [DataField]
    public float Duration = 2.2f;

    /// <summary>С какой высоты (в клетках смещения спрайта) падает капсула.</summary>
    [DataField]
    public float Height = 14f;
}

[Serializable, NetSerializable]
public enum CadiaDropPodLayers : byte
{
    Pod,
    Shadow,
}

/// <summary>Еретек чинит разбитую пусковую шахту.</summary>
[Serializable, NetSerializable]
public sealed partial class CadiaBerthRepairDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
