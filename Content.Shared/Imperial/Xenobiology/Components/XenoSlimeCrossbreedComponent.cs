using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент кроссбридинга ксено-слайма.
/// Взрослый слайм может поглощать экстракты, накапливая заряд для создания ядра.
///
/// Механика:
///   1. Взрослый слайм (IsAdult = true) находит экстракты (XenoSlimeExtractComponent) рядом.
///   2. Поглощает экстракты, накапливая счётчик по цветам.
///   3. При достижении ExtractThreshold штук одного цвета — создаёт XenoChargedSlimeCore.
///   4. После создания ядра слайм гибнет.
/// </summary>
[RegisterComponent]
public sealed partial class XenoSlimeCrossbreedComponent : Component
{
    /// <summary>Сколько экстрактов одного цвета нужно поглотить для создания ядра.</summary>
    [DataField]
    public int ExtractThreshold = 10;

    /// <summary>Радиус поиска экстрактов вокруг слайма (тайлы).</summary>
    [DataField]
    public float ExtractSearchRange = 1.5f;

    /// <summary>Как часто слайм проверяет экстракты рядом (секунд).</summary>
    [DataField]
    public float ScanInterval = 1.0f;

    /// <summary>Таймер до следующего скана.</summary>
    public float ScanTimer = 0f;

    /// <summary>
    /// Счётчики поглощённых экстрактов по цветам.
    /// Рантайм-только: не сериализуется.
    /// </summary>
    public readonly Dictionary<XenoSlimeColor, int> ExtractCounts = new();
}
