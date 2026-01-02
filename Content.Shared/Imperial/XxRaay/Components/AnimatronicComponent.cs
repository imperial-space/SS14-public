using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Networked data for an animatronic entity controlled by the event controller.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class AnimatronicComponent : Component
{
	[DataField("displayName")]
	public string DisplayName = "Animatronic";

	[DataField("moveSpeed")]
	public float MoveSpeed = 2.0f;

	[DataField("targetWaypoint")]
	public EntityUid? TargetWaypoint;

	/// <summary>
	/// Интервал между попытками перерегистрации пути, когда путь недоступен.
	/// </summary>
	[DataField("pathRetryInterval")]
	public TimeSpan PathRetryInterval = TimeSpan.FromSeconds(0.5);

	/// <summary>
	/// Интервал для обновления позиции waypoint'а (если он перемещается).
	/// </summary>
	[DataField("waypointUpdateInterval")]
	public TimeSpan WaypointUpdateInterval = TimeSpan.FromSeconds(0.15);

	/// <summary>
	/// Время последней попытки перерегистрации пути, когда путь был недоступен.
	/// </summary>
	[DataField("lastPathRetryTime")]
	[NonSerialized]
	public TimeSpan? LastPathRetryTime;

	/// <summary>
	/// Время последнего обновления позиции waypoint'а.
	/// </summary>
	[DataField("lastWaypointUpdateTime")]
	[NonSerialized]
	public TimeSpan? LastWaypointUpdateTime;

	/// <summary>
	/// Кулдаун для нанесения урона при касании (в секундах).
	/// </summary>
	[DataField("contactDamageCooldown")]
	public TimeSpan ContactDamageCooldown = TimeSpan.FromSeconds(1.0);

	/// <summary>
	/// Словарь времени последнего нанесения урона каждой цели при касании.
	/// </summary>
	[NonSerialized]
	public Dictionary<EntityUid, TimeSpan> LastContactDamage = new();
}


