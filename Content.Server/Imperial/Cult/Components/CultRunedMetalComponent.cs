using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>
/// Маркер для рунного металла культа.
/// При использовании в руке открывает меню постройки культовых структур.
/// </summary>
[RegisterComponent]
public sealed partial class CultRunedMetalComponent : Component
{
    /// <summary>
    /// Выбранный прототип структуры (после выбора в BUI, до клика на тайл).
    /// </summary>
    public string? SelectedStructureProto;

    /// <summary>
    /// Стоимость выбранной структуры.
    /// </summary>
    public int SelectedStructureCost;

    /// <summary>
    /// Ключ локализации выбранной структуры.
    /// </summary>
    public string SelectedStructureLocKey = string.Empty;
}
