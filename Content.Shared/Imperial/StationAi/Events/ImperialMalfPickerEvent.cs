using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Сервер → клиент: меню модулей (процессорное время, взломанные ЛКП, купленные разовые модули).</summary>
[Serializable, NetSerializable]
public sealed class ImperialMalfPickerEvent(int processingTime, int hackedApcs, List<string> bought, Dictionary<string, int> costs, bool open) : EntityEventArgs
{
    public int ProcessingTime = processingTime;
    public int HackedApcs = hackedApcs;
    public List<string> Bought = bought;

    /// <summary>Цены со скидкой (устройство Судного дня).</summary>
    public Dictionary<string, int> Costs = costs;

    /// <summary>false — только обновить уже открытое окно.</summary>
    public bool Open = open;
}
