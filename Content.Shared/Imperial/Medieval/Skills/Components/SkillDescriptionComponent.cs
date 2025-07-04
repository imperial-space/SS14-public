using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Skills;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SkillDescriptionComponent : Component
{
    [AutoNetworkedField, DataField(required: true)]
    public string SkillId = string.Empty;

    [AutoNetworkedField, DataField(required: true)]
    public string Desc = string.Empty;

    [AutoNetworkedField, DataField(required: true)]
    public int Level = 10;
}
