using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalPlagueGhostComponent : Component
{
    public int Points
    {
        get => _points;
        set
        {
            if (value < 0)
                value = 0;
            _points = value;
        }
    }

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    private int _points = 5;

    [AutoNetworkedField]
    public EntityUid? InfectAction;

    [ViewVariables(VVAccess.ReadWrite)]
    public int FreeInfections = 3;

    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<AlertPrototype> AlertId = "MedievalPlaguePoints";

    public List<EntityUid> Actions = new();
}
