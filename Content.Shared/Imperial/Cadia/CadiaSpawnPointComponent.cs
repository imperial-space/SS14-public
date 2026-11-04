using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Точка высадки стороны: здесь появляются её бойцы.</summary>
[RegisterComponent]
public sealed partial class CadiaSpawnPointComponent : Component
{
    [DataField]
    public CadiaFaction Faction;

    /// <summary>Только для ролей с той же <see cref="CadiaRolePrototype.SpawnGroup"/>.</summary>
    [DataField]
    public string? Group;

    /// <summary>Точка в секторе закрыта, пока за него идёт бой.</summary>
    [DataField]
    public bool Enabled = true;
}
