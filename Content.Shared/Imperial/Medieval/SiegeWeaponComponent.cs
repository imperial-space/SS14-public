using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared.Siege.Components;

[Serializable, NetSerializable]
public enum CatapultVisualKey : byte
{
    Ready
}

[RegisterComponent, AutoGenerateComponentState, NetworkedComponent]
public sealed partial class SiegeWeaponComponent : Component
{
    [DataField]
    public float TargetX = 0;

    [DataField]
    public float TargetY = 0;

    [DataField]
    public float MinTarget = 8;

    [DataField]
    public float MaxTarget = 50;

    [DataField]
    public string LoadedShot = "";

    [DataField, AutoNetworkedField]
    public string AnimationState = "";
    [DataField]
    public float ChargeTime = 5f;

    [DataField]
    public bool SpringCharged = false;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnAim = "/Audio/Imperial/Medieval/Siege/catapult_aim.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnLoad = "/Audio/Imperial/Medieval/Siege/catapult_load.ogg";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnShoot = "/Audio/Imperial/Medieval/Siege/catapult_shot.ogg";
}
