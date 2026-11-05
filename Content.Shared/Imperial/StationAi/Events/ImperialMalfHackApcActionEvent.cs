using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Взломать ЛКП (malfhack): цель — ЛКП.</summary>
public sealed partial class ImperialMalfHackApcActionEvent : WorldTargetActionEvent;
