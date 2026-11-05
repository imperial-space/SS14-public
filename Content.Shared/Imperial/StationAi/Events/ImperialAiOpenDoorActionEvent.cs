using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>open_nearest_door(): ближайший шлюз у отслеживаемой цели.</summary>
public sealed partial class ImperialAiOpenDoorActionEvent : InstantActionEvent;
