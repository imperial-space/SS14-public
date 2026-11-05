using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Перенос ядра в взломанный ЛКП (shunt).</summary>
public sealed partial class ImperialMalfShuntActionEvent : WorldTargetActionEvent;
