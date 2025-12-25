using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Medieval.Skills;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FightForLifeActionComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan? NextUseTime;

    [AutoNetworkedField]
    public EntityUid? ActionEntity;


    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = new()
            {
                { "Blunt", 1 },
                { "Slash", 1 },
                { "Piercing", 1 },
                { "Heat", 1 },
                { "Bloodloss", 4 },
                { "Asphyxiation", 5}
            }
    };
}
