using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Lavaland.Artifact;

/// <summary>
/// Компонент для сущностей, преобразованных в темных слуг
/// Получают урон от света и лечение от темноты
/// </summary>
[RegisterComponent]
public sealed partial class DarkServantComponent : Component
{
    /// <summary>
    /// Урон в секунду от света
    /// </summary>
    [DataField]
    public float LightDamagePerSecond = 2f;

    /// <summary>
    /// Лечение в секунду от темноты
    /// </summary>
    [DataField]
    public float DarknessDamageReduction = 5f;

    /// <summary>
    /// Текущее воздействие света (от 0 до 1)
    /// </summary>
    [ViewVariables]
    public float CurrentLightExposure = 0f;
}
