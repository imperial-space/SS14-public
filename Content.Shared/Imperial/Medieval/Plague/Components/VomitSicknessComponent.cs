using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VomitSicknessComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Duration = 6f;

    [DataField]
    public PlagueVomitLevel Level = PlagueVomitLevel.Slowdown;

    public bool Performed = false;

    public TimeSpan StartTime;
    public TimeSpan EndTime;
}

public enum PlagueVomitLevel : int
{
    Slowdown = 0,
    Vomit = 1,
    Blood = 2
}
