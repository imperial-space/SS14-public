using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

/// <summary>
/// Состояние консоли techweb TG для окна (tgui Techweb): блокировка, очередь изучения сервера
/// и прирост очков в секунду.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialTgTechwebConsoleComponent : Component
{
    /// <summary>toggleLock: заблокированная консоль показывает только окно разблокировки.</summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>queue_nodes: узлы в очереди сервера.</summary>
    [ViewVariables]
    [AutoNetworkedField]
    public List<string> Queue = new();

    /// <summary>Кто поставил узел в очередь (подсказка «Added by»).</summary>
    [ViewVariables]
    [AutoNetworkedField]
    public List<string> QueueBy = new();

    /// <summary>points_last_tick: прирост очков за секунду.</summary>
    [ViewVariables]
    [AutoNetworkedField]
    public int PointsPerSecond;

    /// <summary>sec_protocols: протоколы безопасности (выключаются взломом).</summary>
    [ViewVariables]
    [AutoNetworkedField]
    public bool SecProtocols = true;
}
