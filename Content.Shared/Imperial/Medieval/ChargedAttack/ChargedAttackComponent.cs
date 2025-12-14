using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ChargedAttack;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class ChargedAttackComponent : Component
{
    [ViewVariables, AutoPausedField, AutoNetworkedField]
    public TimeSpan AttackStart;

    [DataField, AutoNetworkedField]
    public float MinAttackTime = 0.5f;

    [DataField, AutoNetworkedField]
    public float MaxAttackTime = 4f;

    [DataField, AutoNetworkedField]
    public float StaticModifier = 1.5f;

    [ViewVariables, AutoNetworkedField]
    public float Modifier;

    [ViewVariables, AutoNetworkedField]
    public bool CurrentAttacking;

    [DataField, AutoNetworkedField]
    public float VectorLenght = 1f;

    [DataField, AutoNetworkedField]
    public float StaminaDamage = 10f;

    [DataField, AutoNetworkedField]
    public float SpeedModifer = 0.6f;

    [ViewVariables, AutoNetworkedField]
    public EntityUid EffectSpawnedEntity = EntityUid.Invalid;

    [DataField, AutoNetworkedField]
    public EntProtoId EffectProtoId;
}

[Serializable, NetSerializable]
public sealed class ChargedAttackStart : EntityEventArgs
{
    public readonly NetEntity Weapon;

    public ChargedAttackStart(NetEntity weapon)
    {
        Weapon = weapon;
    }
}

[Serializable, NetSerializable]
public sealed class ChargedAttackEnd : EntityEventArgs
{
    public readonly NetCoordinates Coordinates;
    public readonly NetEntity Weapon;
    public readonly TimeSpan AttackTime;

    public ChargedAttackEnd(NetCoordinates coordinates, NetEntity weapon, TimeSpan attackTime)
    {
        Coordinates = coordinates;
        Weapon = weapon;
        AttackTime = attackTime;
    }
}