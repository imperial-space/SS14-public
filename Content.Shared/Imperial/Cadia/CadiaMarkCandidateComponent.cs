using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Может принять метку: выдаёт четыре действия выбора, одно на бога.</summary>
[RegisterComponent]
public sealed partial class CadiaMarkCandidateComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Actions = new();
}
