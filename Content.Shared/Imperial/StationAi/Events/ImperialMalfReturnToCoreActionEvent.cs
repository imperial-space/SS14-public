using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Вернуться из ЛКП в ядро (return_to_core).</summary>
public sealed partial class ImperialMalfReturnToCoreActionEvent : InstantActionEvent;
