using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.K9XLunge;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(K9XLungeSystem))]
public sealed partial class K9XLungeComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 5;

    [DataField, AutoNetworkedField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public Vector2? Charge;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField("leapK9XAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string LeapK9XAction = "ActionLeapK9X";
}
