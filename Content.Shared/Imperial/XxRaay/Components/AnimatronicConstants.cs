using System;

namespace Content.Shared.Imperial.XxRaay.Components;

/// <summary>
/// Константы для систем аниматроников.
/// </summary>
public static class AnimatronicConstants
{
	/// <summary>
	/// Расстояние, на котором аниматроник считается достигшим цели (в метрах).
	/// </summary>
	public const float ReachDistance = 0.2f;

	/// <summary>
	/// Урон при контакте с аниматроником.
	/// </summary>
	public const int ContactDamage = 200;

	/// <summary>
	/// Интервал по умолчанию между попытками перерегистрации пути, когда путь недоступен.
	/// </summary>
	public static readonly TimeSpan DefaultPathRetryInterval = TimeSpan.FromSeconds(0.5);

	/// <summary>
	/// Интервал по умолчанию для обновления позиции waypoint'а (если он перемещается).
	/// </summary>
	public static readonly TimeSpan DefaultWaypointUpdateInterval = TimeSpan.FromSeconds(0.15);

	/// <summary>
	/// Кулдаун по умолчанию для нанесения урона при касании (в секундах).
	/// </summary>
	public static readonly TimeSpan DefaultContactDamageCooldown = TimeSpan.FromSeconds(1.0);

	/// <summary>
	/// Время ожидания после регистрации пути перед проверкой статуса (для избежания ложных срабатываний).
	/// </summary>
	public static readonly TimeSpan PathRegistrationWaitTime = TimeSpan.FromSeconds(0.5);

	/// <summary>
	/// Минимальное время ожидания перед повторной проверкой статуса пути после регистрации.
	/// </summary>
	public static readonly TimeSpan MinPathStatusCheckTime = TimeSpan.FromSeconds(0.1);
}

