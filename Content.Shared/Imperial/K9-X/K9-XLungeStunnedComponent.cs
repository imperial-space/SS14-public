using Content.Shared.StatusEffect;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.K9XLunge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(K9XLungeSystem))]
public sealed partial class K9XLungeStunnedComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<StatusEffectPrototype>[] Effects = new ProtoId<StatusEffectPrototype>[] { "Stun", "KnockedDown" };

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpireAt;
}
