using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

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
    [ViewVariables]
    public string? SelectedStructureProto;

    /// <summary>
    /// Стоимость выбранной структуры.
    /// </summary>
    [ViewVariables]
    public int SelectedStructureCost;

    /// <summary>
    /// Ключ локализации выбранной структуры.
    /// </summary>
    [ViewVariables]
    public string SelectedStructureLocKey = string.Empty;
}
