using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPlagueInfectedComponent : Component
{
    public const int MaxProgression = 100;

    [ViewVariables(VVAccess.ReadWrite)]
    public float Progression
    {
        get => _progression;
        set => _progression = Math.Clamp(value, 0, MaxProgression);
    }

    private float _progression = 1;

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool Incubation = true;

    [ViewVariables(VVAccess.ReadWrite)]
    public float UpdatePeriod = 10f;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextProgression = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextCollideSpread = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, BasePlagueEffect> Effects = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public List<IComponent> PlagueComponents = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, BasePlagueEffect> IncubationEffects = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public List<IComponent> IncubationComponents = new();
}
