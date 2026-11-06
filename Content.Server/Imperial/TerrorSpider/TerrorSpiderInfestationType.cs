using Robust.Shared.Random;
using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>TS_INFESTATION_*.</summary>
public enum TerrorSpiderInfestationType : byte
{
    Random,
    Prince,
    White,
    Princess,
    Queen,
}
