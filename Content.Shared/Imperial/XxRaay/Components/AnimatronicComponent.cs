using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Базовый компонент для аниматроника.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class AnimatronicComponent : Component
{
	/// <summary>
	/// Отображаемое имя аниматроника.
	/// </summary>
	[DataField("displayName")]
	public string DisplayName = "Animatronic";

	/// <summary>
	/// Скорость движения аниматроника.
	/// </summary>
	[DataField("moveSpeed")]
	public float MoveSpeed = 2.0f;

	/// <summary>
	/// Текущая цель-waypoint аниматроника.
	/// </summary>
	[DataField("targetWaypoint")]
	public EntityUid? TargetWaypoint;
}


