using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Content.Shared.Imperial.HalloweenCultist;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.HalloweenCultist.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHalloweenCultistSystem))]
public sealed partial class GibCultistRuneComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId MetalProto = "Pumpkinite15";

    [DataField, AutoNetworkedField]
    public EntProtoId MetalProtoHead = "Pumpkinite30";

    [DataField, AutoNetworkedField]
    public EntProtoId EntProto = "MobLilPumpkin";

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Imperial/PumpkinCultist/cultist_effect.ogg");
}