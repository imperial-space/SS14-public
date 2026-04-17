using System.Numerics;
using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент призрака-оператора ксенобиологической консоли.
/// Действия добавляются через ActionGrant в YAML.
/// </summary>
[RegisterComponent]
public sealed partial class XenoConsoleGhostComponent : Component
{
    /// <summary>EntityUid консоли, создавшей этого призрака.</summary>
    public EntityUid ConsoleUid;

    /// <summary>EntityUid разума (Mind) оператора.</summary>
    public EntityUid MindId;

    /// <summary>Оригинальное тело игрока — для TransferTo обратно при выходе.</summary>
    public EntityUid OriginalBodyUid;

    /// <summary>Мировые координаты консоли — точка отсчёта для ограничения дальности.</summary>
    public Vector2 ConsoleWorldPos;

    /// <summary>Максимальное расстояние от консоли в тайлах.</summary>
    public const float MaxRange = 20f;
}
