using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Действие оболочки: вернуться в ядро (undeploy).</summary>
public sealed partial class ImperialAiShellReturnActionEvent : InstantActionEvent;
