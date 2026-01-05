using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Компонент для управления pathfinding и движением аниматроника.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class AnimatronicPathfindingComponent : Component
{
	/// <summary>
	/// Интервал между попытками перерегистрации пути, когда путь недоступен.
	/// </summary>
	[DataField]
	public TimeSpan PathRetryInterval = AnimatronicConstants.DefaultPathRetryInterval;

	/// <summary>
	/// Интервал для обновления позиции waypoint'а (если он перемещается).
	/// </summary>
	[DataField]
	public TimeSpan WaypointUpdateInterval = AnimatronicConstants.DefaultWaypointUpdateInterval;

	/// <summary>
	/// Время последней попытки перерегистрации пути, когда путь был недоступен.
	/// </summary>
	[NonSerialized]
	public TimeSpan? LastPathRetryTime;

	/// <summary>
	/// Время последнего обновления позиции waypoint'а.
	/// </summary>
	[NonSerialized]
	public TimeSpan? LastWaypointUpdateTime;
}

