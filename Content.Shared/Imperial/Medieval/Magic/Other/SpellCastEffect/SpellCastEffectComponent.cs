using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Magic.SpellCastEffect;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpellCastEffectComponent : Component
{
    [DataField(required: true)]

    public EntProtoId EffectProto = string.Empty;
}
