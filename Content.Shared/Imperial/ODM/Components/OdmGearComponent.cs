using Content.Shared.Actions;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.ODM.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class OdmGearComponent : Component
{
    [DataField, AutoNetworkedField]
    public float CurrentGas = 100f;

    [DataField]
    public float MaxGas = 100f;

    [DataField]
    public float HookRange = 12f;

    [DataField]
    public float ReelSpeed = 7f;

    [DataField]
    public float PullForce = 4500f;

    [DataField]
    public float RopeSlack = 0.25f;

    [DataField]
    public float MinRopeLength = 1.2f;

    [DataField]
    public float RopeStiffness = 1.4f;

    [DataField]
    public float RopeBreakPoint = 65000f;

    [DataField]
    public float GasUsage = 0.28f;

    [DataField]
    public float GasUsageIdle = 0.06f;

    [DataField]
    public float ImpactMinimumSpeed = 8f;

    [DataField]
    public float ImpactDamageFactor = 1.2f;

    [DataField]
    public float ImpactCooldown = 1.2f;

    [DataField]
    public float ImpactStunSeconds = 1.5f;

    [DataField]
    public SoundSpecifier? FireSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/harpoon.ogg");

    [DataField]
    public SoundSpecifier? RetractSound = new SoundPathSpecifier("/Audio/Weapons/reel.ogg");

    [DataField]
    public SoundSpecifier? EmptySound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/chamber_empty.ogg");

    [DataField]
    public SoundSpecifier? ImpactSound = new SoundPathSpecifier("/Audio/Effects/metal_slam.ogg");

    [DataField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Blunt";

    [DataField]
    public EntProtoId LeftAction = "ActionOdmLeftHook";

    [DataField]
    public EntProtoId RightAction = "ActionOdmRightHook";

    [DataField]
    public EntProtoId RetractAction = "ActionOdmRetract";

    [DataField]
    public SlotFlags RequiredFlags = SlotFlags.BACK;

    [DataField, AutoNetworkedField]
    public EntityUid? LeftActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? RightActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? RetractActionEntity;

    [DataField]
    public EntityUid? LeftAnchor;

    [DataField]
    public EntityUid? RightAnchor;
}