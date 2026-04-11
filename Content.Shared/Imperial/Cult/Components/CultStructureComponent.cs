using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult.Components;

[Serializable, NetSerializable]
public enum CultStructureType : byte
{
    Altar,
    Forge,
    Archives,
    Pylon,
    RunedAirlock,
    RunedGirder,
}

/// <summary>
/// Культовая структура, построенная из рунного металла.
/// Может быть закреплена/откреплена ритуальным кинжалом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CultStructureComponent : Component
{
    [DataField]
    public CultStructureType StructureType;

    /// <summary>
    /// Время перезарядки структуры после создания предмета.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(4);

    [DataField]
    public TimeSpan NextUse;

    [DataField]
    public TimeSpan NextPylonTick;

    /// <summary>
    /// Скрыта ли структура заклинанием Маскировки присутствия.
    /// </summary>
    [AutoNetworkedField]
    public bool Concealed;

    /// <summary>
    /// Прототип шлюза до преобразования в рунный. Используется для отката при Маскировке.
    /// </summary>
    [DataField]
    public string? OriginalProto;
}
