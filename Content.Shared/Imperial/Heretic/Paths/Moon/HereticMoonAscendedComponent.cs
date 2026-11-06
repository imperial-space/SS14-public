using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

/// <summary>Вознёсшийся еретик Луны: on_life() его ауры тикает раз в жизненный цикл.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonAscendedComponent : Component
{
    /// <summary>COMSIG_LIVING_LIFE: в SS13 Life() идёт раз в 2 секунды.</summary>
    [DataField]
    public TimeSpan AuraInterval = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextAuraTick;
}
