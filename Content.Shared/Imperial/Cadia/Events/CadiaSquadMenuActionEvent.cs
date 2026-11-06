using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Действия ═══════════════════════

/// <summary>Действие визора: открыть окно отрядов.</summary>
public sealed partial class CadiaSquadMenuActionEvent : InstantActionEvent;
