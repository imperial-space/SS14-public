using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Буря посреди поля на время подготовки: стена снега между пилонами (за Империумом) и пустошью высадки (за Хаосом).
/// Пройти сквозь неё нельзя; с началом обстрела, когда завеса падает, буря рассеивается.
/// </summary>
public static class CadiaMidStorm
{
    /// <summary>Клетка-столбец бури: мировая X левого края (поле 240×150, начало в центре; пилоны до X = 38, высадка с X = 64).</summary>
    public const int Column = 55;
    /// <summary>Как широко буря видна по обе стороны столбца.</summary>
    public const float VisualHalfWidth = 7f;
}
