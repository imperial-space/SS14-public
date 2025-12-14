using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Myrmex;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MyrmexQueenComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField] public EntProtoId Egg = "MedievalMyrmexEgg";
}
