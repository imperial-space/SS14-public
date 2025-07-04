using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Skills;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> Levels = new();

    [DataField(serverOnly: true)]
    public Dictionary<string, TimeSpan> Timers = new();

    [ViewVariables]
    public bool LanguagesGain = false;
}
