using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Роль с дополнительными действиями при появлении.</summary>
[RegisterComponent]
public sealed partial class CadiaRoleActionsComponent : Component
{
    [DataField]
    public List<Robust.Shared.Prototypes.EntProtoId> Actions = new();
}
