using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>break_air_alarms (режим затопления одной тревоги).</summary>
public sealed partial class ImperialMalfFloodActionEvent : WorldTargetActionEvent;
