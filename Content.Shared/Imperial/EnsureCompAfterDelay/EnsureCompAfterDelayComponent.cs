using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EnsureCompAfterDelay.Components;

/// <summary>
/// Компонент добавляющий другие компоненты после указанной задержки, после инициализации компонента
/// </summary>
[RegisterComponent, Access(typeof(EnsureCompAfterDelaySystem))]
public sealed partial class EnsureCompAfterDelayComponent : Component
{
    /// <summary>
    /// Компоненты которые будут добавлены после задержки
    /// </summary>
    [DataField("components", required: true)]
    public ComponentRegistry Components = new();

    /// <summary>
    /// Задержка перед добавлением компонента
    /// </summary>
    [DataField("delay")]
    public TimeSpan DelayTime = TimeSpan.FromSeconds(1);
}
