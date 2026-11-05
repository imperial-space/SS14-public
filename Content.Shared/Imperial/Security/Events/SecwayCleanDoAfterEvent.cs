using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

/// <summary>Вычистить банан из трубы.</summary>
[Serializable, NetSerializable]
public sealed partial class SecwayCleanDoAfterEvent : SimpleDoAfterEvent;
