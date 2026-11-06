using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>obj/effect/blessing: святая земля, мешает культовой телепортации и фазовому сдвигу.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultBlessingComponent : Component;
