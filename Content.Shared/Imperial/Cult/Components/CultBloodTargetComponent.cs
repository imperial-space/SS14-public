using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>Кровавая метка мастера (cult_team/blood_target_image): светящийся контур, видимый культистам.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultBloodTargetComponent : Component;
