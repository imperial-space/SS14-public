using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Руна Апокалипсиса: не-культисты видят всех культистами/конструктами, культисты — кровавые искры над неверными.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CultApocalypseVisionComponent : Component
{
    [ViewVariables, AutoNetworkedField] public TimeSpan End;

    /// <summary>Образ для не-гуманоидов: wraith/artificer/juggernaut.</summary>
    [ViewVariables, AutoNetworkedField] public string? Construct;
}
