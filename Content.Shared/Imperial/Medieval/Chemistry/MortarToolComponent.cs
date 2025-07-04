using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Chemistry;

[RegisterComponent, NetworkedComponent]
public sealed partial class MortarToolComponent : Component
{}

[Serializable, NetSerializable]
public sealed partial class MortarDoAfterEvent : SimpleDoAfterEvent {}
