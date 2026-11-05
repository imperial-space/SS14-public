using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

/// <summary>Побег из запертого телепорта гулага (container_resist_act).</summary>
[Serializable, NetSerializable]
public sealed partial class GulagBreakoutDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
