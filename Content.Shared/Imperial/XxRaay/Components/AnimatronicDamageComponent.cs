using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Компонент для управления уроном при контакте аниматроника с другими сущностями.
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class AnimatronicDamageComponent : Component
{
	/// <summary>
	/// Кулдаун для нанесения урона при касании (в секундах).
	/// </summary>
	[DataField]
	public TimeSpan ContactDamageCooldown = AnimatronicConstants.DefaultContactDamageCooldown;

	/// <summary>
	/// Урон при контакте.
	/// </summary>
	[DataField("contactDamage")]
	public int ContactDamage = AnimatronicConstants.ContactDamage;

	/// <summary>
	/// Словарь времени последнего нанесения урона каждой цели при касании.
	/// </summary>
	[NonSerialized]
	public Dictionary<EntityUid, TimeSpan> LastContactDamage = new();
}

