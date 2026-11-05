using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Комиссар: воодушевить бойца — он стоит насмерть.</summary>
public sealed partial class CadiaInspireActionEvent : EntityTargetActionEvent;
