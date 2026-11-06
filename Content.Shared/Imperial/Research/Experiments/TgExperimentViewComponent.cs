using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>Копия прогресса экспериментов сервера на консоли исследований — для окна консоли.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TgExperimentViewComponent : Component
{
    [ViewVariables]
    [AutoNetworkedField]
    public Dictionary<string, int> Progress = new();

    [ViewVariables]
    [AutoNetworkedField]
    public List<string> Completed = new();
}
