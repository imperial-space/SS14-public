using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Lavaland.ColossusLoot;

/// <summary>
/// Shows thermal silhouettes of entities through occluders for the wearer.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThermalEntityVisionComponent : Component;
