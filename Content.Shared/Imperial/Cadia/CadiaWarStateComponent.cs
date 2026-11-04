using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Общее состояние войны для интерфейса: фаза и её конец, пилоны, захват цитадели, сила варпа, следующие волны.
/// Висит на одной глобальной сущности, которую видят все клиенты.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaWarStateComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaWarPhase Phase;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseStart;

    [DataField, AutoNetworkedField]
    public TimeSpan PhaseEnd;

    [DataField, AutoNetworkedField]
    public int PylonsTotal;

    [DataField, AutoNetworkedField]
    public int PylonsAlive;

    /// <summary>0..1 — насколько Хаос захватил цитадель.</summary>
    [DataField, AutoNetworkedField]
    public float CitadelProgress;

    /// <summary>Цитадель сейчас оспаривается (внутри есть обе стороны).</summary>
    [DataField, AutoNetworkedField]
    public bool CitadelContested;

    /// <summary>Сила варпа: растёт с каждым павшим пилоном и убитым защитником, питает призывы Хаоса.</summary>
    [DataField, AutoNetworkedField]
    public int WarpPower;

    [DataField, AutoNetworkedField]
    public TimeSpan NextImperiumWave;

    [DataField, AutoNetworkedField]
    public TimeSpan NextChaosWave;

    [DataField, AutoNetworkedField]
    public CadiaFaction? Winner;

    /// <summary>Сколько мест открыто сейчас: для экрана ожидания и панели ведущего.</summary>
    [DataField, AutoNetworkedField]
    public int ImperiumSeats;

    [DataField, AutoNetworkedField]
    public int ChaosSeats;

    /// <summary>Набор стороны закрыт: перекос или решение ведущего.</summary>
    [DataField, AutoNetworkedField]
    public bool ImperiumClosed;

    [DataField, AutoNetworkedField]
    public bool ChaosClosed;

    /// <summary>Сколько защитников вывезено с падающей Кадии.</summary>
    [DataField, AutoNetworkedField]
    public int EvacSaved;

    /// <summary>Ведущий держит или снял завесу пилонов вручную; null — завеса держится, пока идёт подготовка.</summary>
    [DataField, AutoNetworkedField]
    public bool? VeilOverride;

    /// <summary>Сколько горожан Касра укрыто в катакомбах и сколько их было.</summary>
    [DataField, AutoNetworkedField]
    public int RefugeesSaved;

    [DataField, AutoNetworkedField]
    public int RefugeesTotal;

    [DataField, AutoNetworkedField]
    public TimeSpan NextTransport;

    [DataField, AutoNetworkedField]
    public CadiaWarpStorm Storm;

    [DataField, AutoNetworkedField]
    public TimeSpan StormEnd;

    [DataField, AutoNetworkedField]
    public List<CadiaSectorInfo> Sectors = new();

    [DataField, AutoNetworkedField]
    public List<CadiaLandmark> Landmarks = new();

    [DataField, AutoNetworkedField]
    public CadiaWeather Weather;

    [DataField, AutoNetworkedField]
    public TimeSpan WeatherStart;

    [DataField, AutoNetworkedField]
    public TimeSpan WeatherEnd;
}
