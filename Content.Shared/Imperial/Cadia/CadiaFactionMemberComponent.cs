using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Боец войны за Кадию: сторона и роль. По ним свои видят значки и отряды.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaFactionMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaFaction Faction;

    [DataField, AutoNetworkedField]
    public ProtoId<CadiaRolePrototype>? Role;
}
