using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Quirks;

/// <summary>Причуды, полученные при появлении (для осмотра и эмпатов).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialQuirksComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<TraitPrototype>> Quirks = new();
}
