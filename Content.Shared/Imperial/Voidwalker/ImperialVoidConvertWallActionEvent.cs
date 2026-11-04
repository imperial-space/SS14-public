using Content.Shared.Actions;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Voidwalker;

/// <summary>try_convert_wall(): правый клик TG по стене — здесь отдельное действие.</summary>
public sealed partial class ImperialVoidConvertWallActionEvent : EntityTargetActionEvent;
