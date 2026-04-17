using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Пилон культа. Периодически лечит культистов и конвертирует рядом стоящие структуры.
/// </summary>
[RegisterComponent]
public sealed partial class PylonHealCultistsComponent : Component
{
    /// <summary>Радиус исцеления (тайлы).</summary>
    [DataField]
    public float HealRadius = 5.5f;

    /// <summary>Сколько HP brute лечим за тик.</summary>
    [DataField]
    public float HealBrute = 1f;

    /// <summary>Сколько HP ожогов лечим за тик.</summary>
    [DataField]
    public float HealBurn = 1f;

    /// <summary>Сколько HP кровотечения лечим за тик.</summary>
    [DataField]
    public float HealBloodloss = 1f;

    /// <summary>Интервал между тиками исцеления.</summary>
    [DataField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(3);

    /// <summary>Когда произойдёт следующий тик.</summary>
    [ViewVariables]
    public TimeSpan NextHeal = TimeSpan.Zero;

    /// <summary>
    /// Карта конвертации: ключ — прототип исходной структуры, значение — прототип культового аналога.
    /// </summary>
    [DataField]
    public Dictionary<string, string> ConversionMap = new();

    /// <summary>Радиус конвертации структур (тайлы).</summary>
    [DataField]
    public float ConvertRadius = 3f;

    /// <summary>Интервал между тиками конвертации.</summary>
    [DataField]
    public TimeSpan ConvertInterval = TimeSpan.FromSeconds(30);

    /// <summary>Когда произойдёт следующая конвертация.</summary>
    [ViewVariables]
    public TimeSpan NextConvert = TimeSpan.Zero;
}
