using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Уменьшен (посох/палочка уменьшения).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialShrunkComponent : Component;
