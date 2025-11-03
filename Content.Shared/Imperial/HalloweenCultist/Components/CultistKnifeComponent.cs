using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared.Imperial.HalloweenCultist;
using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHalloweenCultistSystem))]
public sealed partial class CultistKnifeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId RuneProto = "CultistGibRune";

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    [DataField, AutoNetworkedField]
    public EntityUid User;
}
