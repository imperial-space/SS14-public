namespace Content.Server.Imperial.Lavaland.MegafaunaSleep;

/// <summary>
/// Мегафауна спит: стоит на месте, ИИ выключен, способности не применяются.
/// Просыпается, когда живой игрок подходит ближе <see cref="WakeRadius"/> или когда её атакуют;
/// после этого компонент снимается и босс преследует цель как обычно.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandMegafaunaSleepComponent : Component
{
    [DataField]
    public float WakeRadius = 5f;

    /// <summary>Просыпаться от любого полученного урона (выстрел, удар издалека).</summary>
    [DataField]
    public bool WakeOnDamage = true;
}
